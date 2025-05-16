using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using Schedule_for_Un.Models;

namespace Schedule_for_Un.McpServer.Tools;

public class LessonResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public Lesson? Lesson { get; set; }
}

[McpServerToolType]
public class LessonsTools
{
    private readonly ScheduleAPIContext _ctx;

    public LessonsTools(ScheduleAPIContext ctx)
        => _ctx = ctx;
    private IQueryable<Lesson> QueryWithIncludes()
        => _ctx.Lessons
               .Include(l => l.GroupLessons)
                 .ThenInclude(gl => gl.Group)
               .Include(l => l.TeacherLessons)
                 .ThenInclude(tl => tl.Teacher);
    
    private IEnumerable<Lesson> SortLessons(IEnumerable<Lesson> lessons)
        => lessons
           .Where(l => l.Day is >= 1 and <= 5
                    && l.NumberOfPair is >= 1 and <= 4)
           .OrderBy(l => l.Day)
           .ThenBy(l => l.NumberOfPair);
    
    private async Task<IEnumerable<Lesson>> ExecuteAsync(IQueryable<Lesson> query)
        => SortLessons(await query.ToListAsync());

    private IQueryable<Lesson> ApplyDayPairFilter(IQueryable<Lesson> q, int day, int pair)
    {
        if (day > 0 && pair > 0)
            q = q.Where(l => l.Day == day && l.NumberOfPair == pair);
        else if (day > 0)
            q = q.Where(l => l.Day == day);
        else if (pair > 0)
            q = q.Where(l => l.NumberOfPair == pair);
        return q;
    }
    
    private IQueryable<Lesson> ApplyTypeFilter(IQueryable<Lesson> q, string type)
        => type switch
        {
            "лекція"   => q.Where(l => l.IsLecture),
            "практика" => q.Where(l => !l.IsLecture),
            _          => q
        };
    
    private IQueryable<Lesson> ApplyCourseFilter(IQueryable<Lesson> q, int course)
        => course > 0
           ? q.Where(l => l.GroupLessons.Any(gl => gl.Group.Course == course))
           : q;
    
    private IQueryable<Lesson> ApplyWeekFilter(IQueryable<Lesson> q, string week)
    {
        return week switch
        {
            "парний"   => q.Where(l => l.IsEvenWeek == true || l.IsEvenWeek == null),
            "непарний" => q.Where(l => l.IsEvenWeek == false || l.IsEvenWeek == null),
            _          => q
        };
    }
    
    private IQueryable<Lesson> ApplyGroupFilter(IQueryable<Lesson> q, string groupName)
        => string.IsNullOrWhiteSpace(groupName)
           ? q
           : q.Where(l => l.GroupLessons.Any(gl => gl.Group.Name == groupName.Trim()));
    
    private IQueryable<Lesson> ApplyTeacherFilter(IQueryable<Lesson> q, string teacherName)
    {
        if (string.IsNullOrWhiteSpace(teacherName))
            return q.Where(_ => false);

        var tn = teacherName.ToLower().Trim();
        return q.Where(l => l.TeacherLessons
                             .Any(tl => tl.Teacher.FullName
                                            .ToLower()
                                            .Contains(tn)));
    }
    private IQueryable<Lesson> ApplyTimeAndGroupFilter(
        IQueryable<Lesson> query,
        int day,
        int pair,
        string groupName)
    {
        var gp = groupName.Trim();
        query = ApplyDayPairFilter(query, day, pair);
        return query.Where(l =>
            l.GroupLessons.Any(gl =>
                gl.Group.Name.Contains(gp)));
    }

    [McpServerTool, Description(@"
    Returns a complete, sorted list of all lessons in the system, including associated groups and teachers.
    Use this tool when you need an overview of the entire timetable.
    No input parameters are required.
    Lessons are ordered first by day of week (1 = Monday … 5 = Friday), then by pair number (1…4).
    Each Lesson entity includes its GroupLessons→Group and TeacherLessons→Teacher collections.")]
    public Task<IEnumerable<Lesson>> GetAllLessons()
        => ExecuteAsync(QueryWithIncludes());

    [McpServerTool, Description(@"
    Filters lessons by optional day-of-week and pair number.
    Use this tool when you need lessons on a specific day or at a specific lesson period.
    Parameters:
    - day (int): Day of week (1 = Monday … 5 = Friday). Pass 0 to include all days.
    - pair (int): Lesson number (1…4). Pass 0 to include all pairs.
    Returns a sorted list of lessons matching both filters (if specified).")]
    public Task<IEnumerable<Lesson>> GetLessonsFiltered(
        [Description("Day of week: 1–5, or 0 for any.")] int day = 0,
        [Description("Lesson number: 1–4, or 0 for any.")] int pair = 0)
    {
        var q = QueryWithIncludes();
        q = ApplyDayPairFilter(q, day, pair);
        return ExecuteAsync(q);
    }

    [McpServerTool, Description(@"
    Filters lessons by their type: lecture or practice.
    Use this tool when you need only 'лекція' (lectures) or 'практика' (practicals).
    Parameters:
    - type (string): 'лекція' to return only lectures; 'практика' to return only practical sessions.
    Any other value returns all lessons.
    Results are sorted by day and pair.")]
    public Task<IEnumerable<Lesson>> GetLessonsByType(
        [Description("Lesson type: 'лекція' or 'практика'.")] string type)
    {
        var q = QueryWithIncludes();
        q = ApplyTypeFilter(q, type);
        return ExecuteAsync(q);
    }

    [McpServerTool, Description(@"
    Filters lessons by student course year.
    Use this tool when you need the timetable for a particular course (1…4).
    Parameters:
    - course (int): The course number (1–4).
    Pass a non-positive value to include all courses.
    Returns lessons where at least one associated group has the given course.")]
    public Task<IEnumerable<Lesson>> GetLessonsByCourse(
        [Description("Course year: 1–4. Pass 0 or negative for any.")] int course)
    {
        var q = QueryWithIncludes();
        q = ApplyCourseFilter(q, course);
        return ExecuteAsync(q);
    }

    [McpServerTool, Description(@"
    Filters lessons by week parity.
    Use this tool when you need lessons on even or odd weeks.
    Parameters:
    - week (string): 'парний' for even weeks (or unspecified), 'непарний' for odd weeks (or unspecified).
    Any other value returns all lessons.
    Results are sorted by day and pair.")]
    public Task<IEnumerable<Lesson>> GetLessonsByWeek(
        [Description("Week type: 'парний' or 'непарний'.")] string week)
    {
        var q = QueryWithIncludes();
        q = ApplyWeekFilter(q, week);
        return ExecuteAsync(q);
    }

    [McpServerTool, Description(@"
    Filters lessons by both course year and week parity.
    Use this tool when you need, for example, the even-week schedule for a given course.
    Parameters:
    - course (int): Course year (1–4).
    - week (string): 'парний' or 'непарний'.
    Returns lessons matching both criteria, sorted by day and pair.")]
    public Task<IEnumerable<Lesson>> GetLessonsByCourseAndWeek(
        [Description("Course year: 1–4.")] int course,
        [Description("Week type: 'парний' or 'непарний'.")] string week)
    {
        var q = QueryWithIncludes();
        q = ApplyCourseFilter(q, course);
        q = ApplyWeekFilter(q, week);
        return ExecuteAsync(q);
    }

    [McpServerTool, Description(@"
    Filters lessons by a specific student group name.
    Use this tool when you need the timetable for one group.
    Parameters:
    - groupName (string): Exact name of the group (case-sensitive).
    Pass an empty string to include all groups.
    Returns lessons where at least one GroupLesson matches the given name.")]
    public Task<IEnumerable<Lesson>> GetLessonsByGroup(
        [Description("Group name to filter by.")] string groupName)
    {
        var q = QueryWithIncludes();
        q = ApplyGroupFilter(q, groupName);
        return ExecuteAsync(q);
    }

    [McpServerTool, Description(@"
    Filters lessons by both group name and week parity.
    Use this tool when you need, for example, the odd-week schedule for a given group.
    Parameters:
    - groupName (string): Exact group name.
    - week (string): 'парний' or 'непарний'.
    Returns lessons matching both criteria, sorted by day and pair.")]
    public Task<IEnumerable<Lesson>> GetLessonsByGroupAndWeek(
        [Description("Group name to filter by.")] string groupName,
        [Description("Week type: 'парний' or 'непарний'.")] string week)
    {
        var q = QueryWithIncludes();
        q = ApplyGroupFilter(q, groupName);
        q = ApplyWeekFilter(q, week);
        return ExecuteAsync(q);
    }

    [McpServerTool, Description(@"
    Filters lessons by teacher name.
    Use this tool when you need the schedule for a particular instructor.
    Parameters:
    - teacherName (string): Full or partial teacher full name.
    Empty or whitespace input returns an empty result.
    Returns lessons taught by any matching teacher.")]
    public Task<IEnumerable<Lesson>> GetLessonsByTeacher(
        [Description("Full or partial teacher name.")] string teacherName)
    {
        var q = QueryWithIncludes();
        q = ApplyTeacherFilter(q, teacherName);
        return ExecuteAsync(q);
    }

    [McpServerTool, Description(@"
    Filters lessons by teacher name and week parity.
    Use this tool when you need, for example, the even-week timetable for a given instructor.
    Parameters:
    - teacherName (string): Full or partial teacher name.
    - week (string): 'парний' or 'непарний'.
    Returns lessons matching both criteria, sorted by day and pair.")]
    public Task<IEnumerable<Lesson>> GetLessonsByTeacherAndWeek(
        [Description("Full or partial teacher name.")] string teacherName,
        [Description("Week type: 'парний' or 'непарний'.")] string week)
    {
        var q = QueryWithIncludes();
        q = ApplyTeacherFilter(q, teacherName);
        q = ApplyWeekFilter(q, week);
        return ExecuteAsync(q);
    }

    [McpServerTool, Description(@"
    Finds all lessons for a given day, pair, and group name.
    Use when you only know the time slot and group and need to locate matching lessons.
    Parameters:
    - day (int): day of week, 1 = Monday … 5 = Friday
    - pair (int): lesson period, 1 … 4
    - groupName (string): exact or partial group name
    Returns:
    - a sorted list of Lesson objects including associated groups and teachers")]
    public Task<IEnumerable<Lesson>> FindLessonByTimeAndGroup(
        [Description("Day of week: 1–5")] int day,
        [Description("Lesson number: 1–4")] int pair,
        [Description("Group name or substring")] string groupName)
    {
        var q = QueryWithIncludes();
        q = ApplyTimeAndGroupFilter(q, day, pair, groupName);
        return ExecuteAsync(q);
    }
    
    [McpServerTool, Description(@"
    Creates a new lesson and reserves the corresponding free-hour slots.
    You must provide all required fields; any invalid or missing value will cause an error.
    Validation:
    • day must be 1–5, pair must be 1–4  
    • subject must be non-empty  
    • hoursOfSubject ≥ 1  
    • if haveConsultation = true, then consultationHours ≥ 1  
    • you must supply at least one teacherId and one groupId  
    • no group may already have a lesson at that day+pair  
    Behavior:
    • inserts a new Lesson record  
    • creates GroupLesson & TeacherLesson links  
    • marks the teachers' FreeHour entries as occupied  
    Week type mapping:
    • 'even' → only even weeks  
    • 'odd'  → only odd weeks  
    • 'every'→ every week (no parity)  
    Parameters:
    - day (int): 1 = Monday … 5 = Friday  
    - pair (int): lesson period number, 1…4  
    - subject (string): lesson title  
    - hoursOfSubject (int): total hours of the lesson  
    - haveConsultation (bool): whether consultations are included  
    - consultationHours (int): hours of consultations (required if haveConsultation=true)  
    - isLecture (bool): true = lecture, false = practice  
    - weekType (string): 'even', 'odd', or 'every'  
    - teacherIds (List<int>): IDs of instructors (at least one)  
    - groupIds (List<int>): IDs of student groups (at least one)  
    Returns:
    - LessonResult.Success=true with the created Lesson on success  
    - LessonResult.Success=false with an error Message otherwise
    ")]
    public async Task<LessonResult> CreateLesson(
        [Description("Day of week: 1–5")] int day,
        [Description("Lesson number: 1–4")] int pair,
        [Description("Lesson subject")] string subject,
        [Description("Total lesson hours")] int hoursOfSubject,
        [Description("Has consultation?")] bool haveConsultation,
        [Description("Consultation hours")] int consultationHours,
        [Description("Is lecture?")] bool isLecture,
        [Description("Week type: 'even','odd','every'")] string weekType,
        [Description("Teacher IDs")] List<int> teacherIds,
        [Description("Group IDs")] List<int> groupIds
    )
    {
        // Basic validation
        if (teacherIds.Count == 0 || groupIds.Count == 0)
            return new LessonResult {
                Success = false,
                Message = "At least one teacher and one group must be specified."
            };
        if (string.IsNullOrWhiteSpace(subject))
            return new LessonResult {
                Success = false,
                Message = "Subject must be provided."
            };
        if (hoursOfSubject < 1)
            return new LessonResult {
                Success = false,
                Message = "hoursOfSubject must be at least 1."
            };
        if (haveConsultation && consultationHours < 1)
            return new LessonResult {
                Success = false,
                Message = "When haveConsultation=true, consultationHours must be ≥ 1."
            };

        // Parse weekType → bool?
        bool? isEvenWeek;
        switch (weekType.ToLower())
        {
            case "even":
                isEvenWeek = true;
                break;
            case "odd":
                isEvenWeek = false;
                break;
            case "every":
                isEvenWeek = null;
                break;
            default:
                return new LessonResult {
                    Success = false,
                    Message = "Invalid weekType. Must be 'even', 'odd', or 'every'."
                };
        }

        // Check for group conflict at this slot
        var conflict = await _ctx.Lessons
            .Where(l => l.Day == day
                    && l.NumberOfPair == pair
                    && l.GroupLessons.Any(gl => groupIds.Contains(gl.GroupId)))
            .SelectMany(l => l.GroupLessons)
            .FirstOrDefaultAsync(gl => groupIds.Contains(gl.GroupId));
        if (conflict != null)
        {
            var grp = await _ctx.Groups.FindAsync(conflict.GroupId);
            return new LessonResult {
                Success = false,
                Message = $"Group '{grp?.Name}' already has a lesson at day {day}, pair {pair}."
            };
        }

        using var tx = await _ctx.Database.BeginTransactionAsync();
        try
        {
            // Create the Lesson entity
            var lesson = new Lesson
            {
                Day                  = (byte)day,
                NumberOfPair         = (byte)pair,
                Subject              = subject,
                HoursOfSubject       = (byte)hoursOfSubject,
                HaveConsultation     = haveConsultation,
                HoursOfConsultation  = haveConsultation
                                    ? (byte?)consultationHours
                                    : null,
                IsLecture            = isLecture,
                IsEvenWeek           = isEvenWeek
            };
            _ctx.Lessons.Add(lesson);
            await _ctx.SaveChangesAsync();

            // Link to groups and teachers
            _ctx.GroupLessons.AddRange(groupIds
                .Select(gid => new GroupLesson { LessonId = lesson.Id, GroupId = gid }));
            _ctx.TeacherLessons.AddRange(teacherIds
                .Select(tid => new TeacherLesson { LessonId = lesson.Id, TeacherId = tid }));
            await _ctx.SaveChangesAsync();

            // Occupy FreeHour slots
            var freeHrs = await _ctx.FreeHours
                .Where(fh => teacherIds.Contains(fh.TeacherId)
                        && fh.Day == day
                        && fh.NumberOfPair == pair)
                .ToListAsync();
            freeHrs.ForEach(fh => {
                fh.IsFree   = false;
                fh.LessonId = lesson.Id;
            });
            await _ctx.SaveChangesAsync();

            await tx.CommitAsync();
            return new LessonResult {
                Success = true,
                Lesson  = lesson
            };
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    [McpServerTool, Description(@"
    Updates one or more fields of an existing lesson and adjusts the corresponding freeHour slots.
    You may specify only the fields you wish to change; any parameter left at its default value is not modified.
    Validation:
    • lessonId must refer to an existing lesson
    • if day, pair, or groupIds change, no group may already have a lesson at that slot
    • all provided groupIds must belong to the same course and specialty
    • each provided teacherId must have a free slot at the final day+pair
    • if haveConsultation='true', then consultationHours ≥ 1
    Behavior:
    • frees old FreeHour slots for affected teachers if day/pair or teacherIds change
    • updates only the entity properties whose parameters differ from defaults
    • replaces GroupLesson/TeacherLesson links if non‐empty lists are provided
    • occupies new FreeHour slots for the final teacher/day/pair combination
    Parameters:
    - lessonId (int): ID of the lesson to update (required)
    - day (int): new day (1–5), or 0 to keep existing
    - pair (int): new pair (1–4), or 0 to keep existing
    - subject (string): new subject, or empty to keep existing
    - hoursOfSubject (int): new total hours (>0), or 0 to keep existing
    - haveConsultation (string): 'true' or 'false' to change, or empty to keep existing
    - consultationHours (int): new consultation hours (>0), or 0 to keep existing
    - isLecture (string): 'true' or 'false' to change, or empty to keep existing
    - weekType (string): 'even','odd','every' to change, or empty to keep existing
    - teacherIds (List<int>): new teacher IDs, or empty list to keep existing
    - groupIds   (List<int>): new group IDs, or empty list to keep existing
    Returns:
    - LessonResult.Success=true and updated Lesson on success  
    - LessonResult.Success=false and Message on error
    ")]
    public async Task<LessonResult> UpdateLesson(
        [Description("ID of the lesson to update")] int lessonId,
        [Description("New day (1–5), or 0 to keep existing")] int day = 0,
        [Description("New pair (1–4), or 0 to keep existing")] int pair = 0,
        [Description("New subject, or empty to keep existing")] string subject = "",
        [Description("New total hours (>0), or 0 to keep existing")] int hoursOfSubject = 0,
        [Description("New haveConsultation: 'true'/'false', or empty to keep existing")] string haveConsultation = "",
        [Description("New consultation hours (>0), or 0 to keep existing")] int consultationHours = 0,
        [Description("New isLecture: 'true'/'false', or empty to keep existing")] string isLecture = "",
        [Description("Week type: 'even'/'odd'/'every', or empty to keep existing")] string weekType = "",
        [Description("New Teacher IDs, or empty list to keep existing")] List<int>? teacherIds = default,
        [Description("New Group IDs, or empty list to keep existing")] List<int>? groupIds = default)
    {
        // 1. Load existing lesson
        var existing = await _ctx.Lessons
        .Include(l => l.TeacherLessons)
        .Include(l => l.GroupLessons)
        .FirstOrDefaultAsync(l => l.Id == lessonId);
        if (existing == null)
        return new LessonResult { Success = false, Message = $"Lesson ID={lessonId} not found." };

        // 2. Determine changes
        bool dayChanged = day > 0 && day != existing.Day;
        bool pairChanged = pair > 0 && pair != existing.NumberOfPair;
        bool subjectChanged = !string.IsNullOrEmpty(subject) && subject != existing.Subject;
        bool hoursChanged = hoursOfSubject > 0 && hoursOfSubject != existing.HoursOfSubject;
        bool? parsedHaveConsultation = haveConsultation.ToLower() switch { "true" => true, "false" => false, _ => (bool?)null };
        bool consultFlagChanged = parsedHaveConsultation.HasValue && parsedHaveConsultation.Value != existing.HaveConsultation;
        bool hoursConsultChanged = consultationHours > 0 && consultationHours != (existing.HoursOfConsultation ?? 0);
        bool? parsedIsLecture = isLecture.ToLower() switch { "true" => true, "false" => false, _ => (bool?)null };
        bool lectureChanged = parsedIsLecture.HasValue && parsedIsLecture.Value != existing.IsLecture;
        bool weekChanged = !string.IsNullOrEmpty(weekType);
        bool? parsedWeek = weekType.ToLower() switch { "even" => true, "odd" => false, "every" => (bool?)null, _ => (bool?)null };
        bool teachersChanged = teacherIds != null && teacherIds.Any();
        bool groupsChanged = groupIds != null && groupIds.Any();

        // 3. Compute final values
        int finalDay = dayChanged ? day : existing.Day;
        int finalPair = pairChanged ? pair : existing.NumberOfPair;
        string finalSubject = subjectChanged ? subject : existing.Subject;
        int finalHours = hoursChanged ? hoursOfSubject : existing.HoursOfSubject;
        bool finalHaveConsultation = consultFlagChanged ? parsedHaveConsultation.Value : existing.HaveConsultation;
        int? finalConsultHours = hoursConsultChanged ? consultationHours : existing.HoursOfConsultation;
        bool finalIsLecture = lectureChanged ? parsedIsLecture.Value : existing.IsLecture;
        bool? finalWeek = weekChanged ? parsedWeek : existing.IsEvenWeek;
        var finalTeacherIds = teachersChanged ? teacherIds! : existing.TeacherLessons.Select(tl => tl.TeacherId).ToList();
        var finalGroupIds = groupsChanged ? groupIds! : existing.GroupLessons.Select(gl => gl.GroupId).ToList();

        // 4. Validate consultations
        if (finalHaveConsultation && (!finalConsultHours.HasValue || finalConsultHours < 1))
        return new LessonResult { Success = false, Message = "Consultation hours ≥ 1 required when consultations enabled." };

        // 5. Ensure all groups share same course & specialty
        if (groupsChanged)
        {
        var groups = await _ctx.Groups.Where(g => finalGroupIds.Contains(g.Id)).ToListAsync();
        var courses = groups.Select(g => g.Course).Distinct().Count();
        var specs = groups.Select(g => g.Specialty).Distinct().Count();
        if (courses > 1 || specs > 1)
        return new LessonResult {
            Success = false,
            Message = "All groups must be from the same course and specialty."
        };
        }

        // 6. Check group-slot conflicts
        if (dayChanged || pairChanged || groupsChanged)
        {
        var conflict = await _ctx.Lessons
        .Where(l => l.Id != lessonId && l.Day == finalDay && l.NumberOfPair == finalPair)
        .SelectMany(l => l.GroupLessons)
        .FirstOrDefaultAsync(gl => finalGroupIds.Contains(gl.GroupId));
        if (conflict != null)
        {
        var grp = await _ctx.Groups.FindAsync(conflict.GroupId);
        return new LessonResult {
            Success = false,
            Message = $"Group '{grp?.Name}' already has a lesson at day {finalDay}, pair {finalPair}."
        };
        }
        }

        // 7. Check each teacher is free
        if (dayChanged || pairChanged || teachersChanged)
        {
        foreach (var tid in finalTeacherIds)
        {
        bool free = await _ctx.FreeHours.AnyAsync(fh =>
            fh.TeacherId == tid &&
            fh.Day == finalDay &&
            fh.NumberOfPair == finalPair &&
            fh.IsFree);
        if (!free)
            return new LessonResult {
            Success = false,
            Message = $"Teacher ID={tid} is not free at day {finalDay}, pair {finalPair}."
            };
        }
        }

        using var tx = await _ctx.Database.BeginTransactionAsync();
        try
        {
        // 8. Free old slots
        if (dayChanged || pairChanged || teachersChanged)
        {
        var oldT = existing.TeacherLessons.Select(tl => tl.TeacherId).ToList();
        var oldSlots = await _ctx.FreeHours
            .Where(fh => oldT.Contains(fh.TeacherId) && fh.LessonId == lessonId)
            .ToListAsync();
        oldSlots.ForEach(fh => { fh.IsFree = true; fh.LessonId = null; });
        await _ctx.SaveChangesAsync();
        }

        // 9. Apply field changes
        if (dayChanged) existing.Day = (byte)finalDay;
        if (pairChanged) existing.NumberOfPair = (byte)finalPair;
        if (subjectChanged) existing.Subject = finalSubject;
        if (hoursChanged) existing.HoursOfSubject = (byte)finalHours;
        if (consultFlagChanged) existing.HaveConsultation = finalHaveConsultation;
        if (hoursConsultChanged) existing.HoursOfConsultation = finalConsultHours.HasValue ? (byte?)finalConsultHours.Value : null;
        if (lectureChanged) existing.IsLecture = finalIsLecture;
        if (weekChanged) existing.IsEvenWeek = finalWeek;
        await _ctx.SaveChangesAsync();

        // 10. Update group links
        if (groupsChanged)
        {
        _ctx.GroupLessons.RemoveRange(existing.GroupLessons);
        _ctx.GroupLessons.AddRange(finalGroupIds.Select(gid => new GroupLesson { LessonId = lessonId, GroupId = gid }));
        await _ctx.SaveChangesAsync();
        }

        // 11. Update teacher links
        if (teachersChanged)
        {
        _ctx.TeacherLessons.RemoveRange(existing.TeacherLessons);
        _ctx.TeacherLessons.AddRange(finalTeacherIds.Select(tid => new TeacherLesson { LessonId = lessonId, TeacherId = tid }));
        await _ctx.SaveChangesAsync();
        }

        // 12. Occupy new slots
        if (dayChanged || pairChanged || teachersChanged)
        {
        var newSlots = await _ctx.FreeHours
            .Where(fh => finalTeacherIds.Contains(fh.TeacherId) && fh.Day == finalDay && fh.NumberOfPair == finalPair)
            .ToListAsync();
        newSlots.ForEach(fh => { fh.IsFree = false; fh.LessonId = lessonId; });
        await _ctx.SaveChangesAsync();
        }

        await tx.CommitAsync();
        return new LessonResult { Success = true, Lesson = existing };
        }
        catch
        {
        await tx.RollbackAsync();
        throw;
        }
    }

    [McpServerTool, Description(@"
    Deletes a lesson by its unique ID and releases all associated free-hour slots.
    This tool should be used with caution and only after explicit confirmation.
    Also this tool works with the ConfirmDeleteLesson prompt, which handles proper verification before deletion.
    Parameters:
    - lessonId (int): ID of the lesson to delete
    - confirmationToken (string): must be 'yes' to proceed
    Returns:
    - LessonResult.Success=true and Lesson (deleted) on success
    - LessonResult.Success=false and Message on error")]
    public async Task<LessonResult> DeleteLesson(
            [Description("Lesson ID to remove")] int lessonId,
            [Description("Confirmation token, must be 'yes'")] string confirmationToken)
    {
        if (confirmationToken != "yes")
            return new LessonResult {
                Success = false,
                Message = "Видалення скасовано."
            };

        var L = await _ctx.Lessons.FindAsync(lessonId);
        if (L == null)
            return new LessonResult {
                Success = false,
                Message = $"Пари з ID={lessonId} не знайдено."
            };

        using var tx = await _ctx.Database.BeginTransactionAsync();
        try
        {
            // Звільняємо слоти викладачів
            var teacherIds = await _ctx.TeacherLessons
                .Where(tl => tl.LessonId == lessonId)
                .Select(tl => tl.TeacherId)
                .ToListAsync();
            var frees = await _ctx.FreeHours
                .Where(fh => teacherIds.Contains(fh.TeacherId)
                        && fh.LessonId == lessonId)
                .ToListAsync();
            frees.ForEach(fh => { fh.IsFree = true; fh.LessonId = null; });
            await _ctx.SaveChangesAsync();

            // Видаляємо зв'язки
            _ctx.TeacherLessons.RemoveRange(
                _ctx.TeacherLessons.Where(tl => tl.LessonId == lessonId));
            _ctx.GroupLessons.RemoveRange(
                _ctx.GroupLessons.Where(gl => gl.LessonId == lessonId));
            await _ctx.SaveChangesAsync();

            // Видаляємо саму пару
            _ctx.Lessons.Remove(L);
            await _ctx.SaveChangesAsync();

            await tx.CommitAsync();
            return new LessonResult {
                Success = true,
                Lesson  = L
            };
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    [McpServerTool, Description(@"
    Deletes lessons in bulk, optionally filtered by student course and/or day of week.
    Use this tool to remove multiple lessons at once:
    • course=0 & day=0 → deletes all lessons  
    • course>0 & day=0 → deletes all lessons for that course  
    • course=0 & day>0 → deletes all lessons on that day  
    • course>0 & day>0 → deletes all lessons for that course on that day  
    Validation:
    • course must be between 1 and 4, or 0 to ignore  
    • day must be between 1 (Monday) and 5 (Friday), or 0 to ignore  
    Behavior:
    • frees any occupied FreeHour slots for the affected lessons teachers  
    • removes all TeacherLesson and GroupLesson links  
    • deletes the Lesson records  
    Returns:
    - Success=true with DeletedCount on success  
    - Success=false with an explanatory Message if no lessons found or inputs invalid
    ")]
    public async Task<LessonResult> DeleteLessons(
        [Description("Student course (1–4), or 0 to ignore")] int course = 0,
        [Description("Day of week (1–5), or 0 to ignore")]    int day    = 0
    )
    {
        if (course < 0 || course > 4)
            return new LessonResult {
                Success = false,
                Message = $"Invalid course: {course}. Must be 1–4 or 0."
            };
        if (day < 0 || day > 5)
            return new LessonResult {
                Success = false,
                Message = $"Invalid day: {day}. Must be 1–5 or 0."
            };

        // 1. Build the query using existing helpers
        var q = QueryWithIncludes();
        q = ApplyCourseFilter(q, course);
        q = ApplyDayPairFilter(q, day, 0);

        // 2. Fetch matching lessons
        var lessons = await q.ToListAsync();
        if (!lessons.Any())
            return new LessonResult {
                Success = false,
                Message = "No lessons found matching the specified filters."
            };

        var lessonIds = lessons.Select(l => l.Id).ToList();

        using var tx = await _ctx.Database.BeginTransactionAsync();
        try
        {
            // 3. Free up any occupied FreeHour slots
            var slots = await _ctx.FreeHours
                .Where(fh => fh.LessonId.HasValue && lessonIds.Contains(fh.LessonId.Value))
                .ToListAsync();
            slots.ForEach(fh => { fh.IsFree = true; fh.LessonId = null; });
            await _ctx.SaveChangesAsync();

            // 4. Remove all TeacherLesson & GroupLesson links
            _ctx.TeacherLessons.RemoveRange(
                _ctx.TeacherLessons.Where(tl => lessonIds.Contains(tl.LessonId))
            );
            _ctx.GroupLessons.RemoveRange(
                _ctx.GroupLessons.Where(gl => lessonIds.Contains(gl.LessonId))
            );
            await _ctx.SaveChangesAsync();

            // 5. Delete the Lesson records
            _ctx.Lessons.RemoveRange(lessons);
            await _ctx.SaveChangesAsync();

            await tx.CommitAsync();
            return new LessonResult {
                Success = true,
                Message = $"{lessons.Count} lesson(s) deleted."
            };
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }
}