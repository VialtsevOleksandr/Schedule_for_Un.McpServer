using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using Schedule_for_Un.Models;

namespace Schedule_for_Un.McpServer.Tools;

[McpServerToolType]
public class TeachersTools
{
    private readonly ScheduleAPIContext _ctx;

    public TeachersTools(ScheduleAPIContext ctx)
    {
        _ctx = ctx;
    }
    private string GetDayName(int day)
    {
        return day switch
        {
            1 => "Понеділок",
            2 => "Вівторок",
            3 => "Середа",
            4 => "Четвер",
            5 => "П'ятниця",
            _ => "Невідомий день"
        };
    }
    public class FreeHourModel
    {
        public byte Day { get; set; }
        public byte NumberOfPair { get; set; }
    }
    public List<string> ValidTeacherPositions() => new() { "Асистент", "Доцент", "Професор" };

    //[McpServerTool, Description("Знайти всіх викладачів")]
    [McpServerTool, Description(
    "Returns a full list of all teachers in the system. " +
    "Use this tool when you need to fetch the complete set of registered teachers." +
    "No input parameters are required. Returns a list of Teacher objects.")]
    public async Task<IEnumerable<Teacher>> GetAllTeachers()
    {
        var list = await _ctx.Teachers.ToListAsync();
        return list;
    }

    // [McpServerTool,
    //  Description(
    //      "Знайти вільних викладачів за опційними параметрами:\n" +
    //      "- day: день тижня (1=понеділок … 7=неділя) або 0 для всіх днів\n" +
    //      "- pair: номер пари (1…n) або 0 для всіх пар"
    // )]
    [McpServerTool, Description(
    "Finds teachers who are available (i.e., have free time slots) based on optional filtering parameters. " +
    "Use this tool when the user wants to know which teachers are free, free during a specific day of the week (1 = Monday … 5 = Friday) and/or a specific pair (lesson period). " +
    "Parameters:" +
    "- day (int): Day of the week. Use 0 to ignore the day filter." +
    "- pair (int): Lesson number (e.g., 1st pair, 2nd pair). Use 0 to ignore the pair filter." +
    "The tool returns teachers who have at least one matching free hour. Internally, it performs a filtered query over teacher free hour slots."
    )]
    public async Task<IEnumerable<Teacher>> GetAvailableTeachers(int day = 0, int pair = 0)
    {
        var query = _ctx.Teachers.AsQueryable();

        if (day > 0 && pair > 0)
        {
            query = query.Where(t => t.FreeHours.Any(fh => fh.IsFree && fh.Day == day && fh.NumberOfPair == pair));
        }
        else if (day > 0)
        {
            query = query.Where(t => t.FreeHours.Any(fh => fh.IsFree && fh.Day == day));
        }
        else if (pair > 0)
        {
            query = query.Where(t => t.FreeHours.Any(fh => fh.IsFree && fh.NumberOfPair == pair));
        }
        else
        {
            query = query.Where(t => t.FreeHours.Any(fh => fh.IsFree));
        }

        var teachers = await query
            .Include(t => t.FreeHours.Where(fh =>
                fh.IsFree &&
                (day == 0 || fh.Day == day) &&
                (pair == 0 || fh.NumberOfPair == pair)))
            .ToListAsync();

        return teachers;
    }

    //[McpServerTool, Description("Знайти всі вільні години викладача за ПІБ або прізвищем")]
    [McpServerTool, Description(
    "Finds all available (free) time slots for a specific teacher, based on their full name or surname. " +
    "Use this tool when a user asks something like 'When is Professor Shevchenko free?' or 'Show free hours for teacher Ivanenko'. " +
    "Parameter:" +
    "- teacherName (string): The full name or part of the name/surname of the teacher." +
    "If no teachers are found, or multiple teachers match the name, the tool returns a message with suggestions. " +
    "If exactly one teacher is found, their free hours are returned grouped by day of the week, including day names and lesson numbers. " +
    "Handles edge cases: empty input, no match, or multiple matches."
    )]
    public async Task<object> GetTeacherFreeHours(string teacherName)
    {
        if (string.IsNullOrWhiteSpace(teacherName))
        {
            return new { Success = false, Message = "Будь ласка, вкажіть ПІБ або прізвище викладача" };
        }

        var teacherNameLower = teacherName.ToLower().Trim();
        var matchingTeachers = await _ctx.Teachers
            .Where(t => t.FullName.ToLower().Contains(teacherNameLower))
            .ToListAsync();

        if (!matchingTeachers.Any())
        {
            return new { Success = false, Message = $"Викладача з ім'ям '{teacherName}' не знайдено в базі даних" };
        }
        if (matchingTeachers.Count > 1)
        {
            return new 
            { 
                Success = false, 
                Message = "Знайдено кілька викладачів. Будь ласка, уточніть своє запитання.",
                Teachers = matchingTeachers.Select(t => new { Id = t.Id, FullName = t.FullName, Position = t.Position })
            };
        }
        var teacher = matchingTeachers.First();
        
        var teacherWithFreeHours = await _ctx.Teachers
            .Where(t => t.Id == teacher.Id)
            .Include(t => t.FreeHours.Where(fh => fh.IsFree))
            .FirstOrDefaultAsync();

        if (teacherWithFreeHours == null || !teacherWithFreeHours.FreeHours.Any())
        {
            return new 
            { 
                Success = true, 
                Teacher = new { Id = teacher.Id, FullName = teacher.FullName, Position = teacher.Position },
                Message = "Для цього викладача не знайдено вільних годин"
            };
        }
        var groupedHours = teacherWithFreeHours.FreeHours
            .GroupBy(fh => fh.Day)
            .OrderBy(g => g.Key)
            .Select(g => new
            {
                Day = g.Key,
                DayName = GetDayName(g.Key),
                Hours = g.OrderBy(fh => fh.NumberOfPair)
                    .Select(fh => new { Pair = fh.NumberOfPair, Id = fh.Id })
            });

        return new 
        { 
            Success = true, 
            Teacher = new { Id = teacher.Id, FullName = teacher.FullName, Position = teacher.Position },
            FreeHours = groupedHours
        };
    }
    
    //[McpServerTool, Description("Знайти викладача за ПІБ або прізвищем")]

    [McpServerTool, Description(
    "Searches for teachers by full name or surname. Use this when a user provides a name fragment and wants to determine all teacher information or select relevant teachers. " +
    "Parameter:" +
    "- teacherName (string): Part or full teacher name." +
    "Returns a list of all teachers whose name contains the entered value, case insensitive. " +
    "Does not filter based on availability. This tool is useful for name recognition or pre-screening."
    )]
    public async Task<IEnumerable<Teacher>> GetTeacherByName(string teacherName)
    {
        var teacherNameLower = teacherName.ToLower().Trim();
        var matchingTeachers = await _ctx.Teachers
            .Where(t => t.FullName.ToLower().Contains(teacherNameLower))
            .Include(t => t.FreeHours)
            .ToListAsync();

        return matchingTeachers;
    }

    [McpServerTool, Description(@"
    Creates a new teacher with the provided full name, position, and optionally their available time slots.
    This tool performs strict validation:
    • Ensures the full name is in format 'Surname I. O.' and unique across the database.
    • Validates the position against the allowed values: 'Асистент', 'Доцент', 'Професор'.
    • Accepts an optional list of available hours. If not provided, assumes the teacher is available for all pairs on all days.
    • Automatically associates generated FreeHour entries with the teacher, all marked as available (IsFree = true).
    Parameters:
    - fullName: string, required, format 'Surname I. O.'
    - position: string, must be one of the accepted types.
    - optionalFreeHours: list of tuples (Day, NumberOfPair), optional.
    - if the user has not specified it explicitly, pass an empty list: `[]`. Do not omit the parameter or pass null in this case.
    Returns success message and created Teacher object if successful; otherwise returns descriptive validation errors.")]
    public async Task<object> CreateTeacher(
        [Description("Teacher full name, must be unique. Format: 'Surname I. O.'")] string fullName,
        [Description("Position: must be 'Асистент', 'Доцент' or 'Професор'")] string position,
        [Description("Optional list of available (free) hours. If omitted, all hours will be marked free by default, pass an empty list: `[]` if the user has not specified it explicitly")] List<(byte Day, byte NumberOfPair)>? optionalFreeHours = default)
    {

        if (string.IsNullOrWhiteSpace(fullName))
            return new { Success = false, Message = "ПІБ викладача не може бути порожнім." };

        if (!ValidTeacherPositions().Contains(position))
            return new
            {
                Success = false,
                Message = $"Невірна позиція: '{position}'. Допустимі: {string.Join(", ", ValidTeacherPositions())}."
            };

        var trimmedName = fullName.Trim();
        var duplicate = await _ctx.Teachers.AnyAsync(t => t.FullName == trimmedName);
        if (duplicate)
            return new { Success = false, Message = $"Викладач з ім'ям '{trimmedName}' вже існує." };

        var teacher = new Teacher
        {
            FullName = trimmedName,
            Position = position
        };

        _ctx.Teachers.Add(teacher);
        await _ctx.SaveChangesAsync();

        // Create FreeHour for the teacher
        var hours = new List<FreeHour>();
        if (optionalFreeHours != default && optionalFreeHours.Any())
        {
            foreach (var (day, pair) in optionalFreeHours)
            {
                hours.Add(new FreeHour
                {
                    Day = day,
                    NumberOfPair = pair,
                    IsFree = true,
                    TeacherId = teacher.Id
                });
            }
        }
        else
        {
            for (byte d = 1; d <= 5; d++)
            for (byte p = 1; p <= 4; p++)
            {
                hours.Add(new FreeHour
                {
                    Day = d,
                    NumberOfPair = p,
                    IsFree = true,
                    TeacherId = teacher.Id
                });
            }
        }

        _ctx.FreeHours.AddRange(hours);
        await _ctx.SaveChangesAsync();

        return new { Success = true, Message = "Викладача створено успішно", Teacher = teacher };
    }

    [McpServerTool, 
    Description(@"
    Updates an existing teacher's details and availability(freeHours) in a flexible, safe way. 
    This tool allows:
    • Identifying the teacher by exact 'currentFullName' (e.g., 'Свистунов О. А.').
    • Updating their full name and/or academic position, with validation for uniqueness and allowed positions.
    • Fine-grained control over available hours (FreeHours) — without blindly clearing all existing entries.

    Key behavior for updating FreeHours:
    • You can remove specific available time slots (via 'freeHoursToRemove') — but it will *prevent* deletion if any of those slots are already occupied (IsFree == false), protecting scheduling integrity.
    • You can add new time slots (via 'freeHoursToAdd') — only slots that don't already exist (with IsFree == true) will be added, avoiding duplicates.

    Validation:
    - Full name must be unique and in format: 'Surname I. O.'
    - Position must be one of: 'Асистент', 'Доцент', 'Професор'
    - Days must be 1–5, NumberOfPair must be within expected schedule bounds (e.g., 1–4)

    Parameters:
    - currentFullName (required): Exact current name of the teacher to find.
    - newFullName (optional): A new full name. Must be unique if provided.
    - newPosition (optional): One of the allowed academic positions.
    - freeHoursToAdd (optional): List of (Day, NumberOfPair) to mark as free if not already present.
    - freeHoursToRemove (optional): List of (Day, NumberOfPair) to remove *only if* not associated with a lesson (i.e., still free).

    Context for the LLM:
    - Supports interactive updates like: 
        'Add free hours on Monday for the second and third pairs for Свистунов O.A.' or 'Remove free hours on Monday for the second and third pairs for Свистунов O.A.' or 
        'Remove free hours on Thursday for teacher Свистунов O.A.' or 'Change position for Свистунов O.A. to associate professor'

    Returns:
    - Updated Teacher object and a descriptive message of what was changed or prevented.")]
    public async Task<object> UpdateTeacher(
        [Description("Exact full name of the teacher to update")] string currentFullName,
        [Description("Optional new full name, pass an empty string: `` if the user has not specified it explicitly")] string newFullName = "",
        [Description("Optional new position (Асистент, Доцент, Професор), pass an empty string: `` if the user has not specified it explicitly")] string newPosition = "",
        [Description("FreeHours to add (optional), pass an empty list: `[]` if the user has not specified it explicitly")] List<FreeHourModel>? freeHoursToAdd = default,
        [Description("FreeHours to be deleted (optional), pass an empty list: `[]` if the user has not specified it explicitly")] List<FreeHourModel>? freeHoursToRemove = default)
    {
        var teacher = await _ctx.Teachers
            .Include(t => t.FreeHours)
            .FirstOrDefaultAsync(t => t.FullName == currentFullName.Trim());

        if (teacher == null)
            return new { Success = false, Message = $"Викладача з ім’ям '{currentFullName}' не знайдено." };

        bool isChanged = false;

        if (!string.IsNullOrWhiteSpace(newFullName))
        {
            var trimmed = newFullName.Trim();
            if (trimmed != teacher.FullName)
            {
                bool exists = await _ctx.Teachers.AnyAsync(t => t.FullName == trimmed && t.Id != teacher.Id);
                if (exists)
                    return new { Success = false, Message = $"Викладач з ім’ям '{trimmed}' вже існує." };

                teacher.FullName = trimmed;
                isChanged = true;
            }
        }

        if (!string.IsNullOrWhiteSpace(newPosition))
        {
            if (!ValidTeacherPositions().Contains(newPosition))
                return new { Success = false, Message = $"Недопустима позиція: '{newPosition}'. Допустимі: {string.Join(", ", ValidTeacherPositions())}." };

            if (teacher.Position != newPosition)
            {
                teacher.Position = newPosition;
                isChanged = true;
            }
        }

        if (freeHoursToAdd != default && freeHoursToAdd.Any())
        {
            foreach (var item in freeHoursToAdd)
            {
                var existingHour = await _ctx.FreeHours
                    .FirstOrDefaultAsync(h => h.TeacherId == teacher.Id && 
                                            h.Day == item.Day && 
                                            h.NumberOfPair == item.NumberOfPair);

                if (existingHour == null)
                {
                    _ctx.FreeHours.Add(new FreeHour
                    {
                        Day = item.Day,
                        NumberOfPair = item.NumberOfPair,
                        IsFree = true,
                        TeacherId = teacher.Id
                    });
                    isChanged = true;
                }
            }
        }

        if (freeHoursToRemove != default && freeHoursToRemove.Any())
        {
            foreach (var item in freeHoursToRemove)
            {
                var fh = await _ctx.FreeHours
                    .FirstOrDefaultAsync(h => h.TeacherId == teacher.Id && 
                                            h.Day == item.Day && 
                                            h.NumberOfPair == item.NumberOfPair);

                if (fh != null)
                {
                    if (!fh.IsFree)
                        return new { Success = false, Message = $"Не можна видалити годину {item.Day}-го дня, пара {item.NumberOfPair}, оскільки вона вже зайнята." };

                    _ctx.FreeHours.Remove(fh);
                    isChanged = true;
                }
            }
        }

        if (!isChanged)
            return new { Success = true, Message = "Дані викладача не змінено.", Teacher = teacher };

        await _ctx.SaveChangesAsync();
        return new { Success = true, Message = "Дані викладача успішно оновлено.", Teacher = teacher };
    }

    [McpServerTool, Description(@"
    Deletes a teacher from the system by their unique ID. This tool should be used with caution and only after confirmation.
    This tool works with the ConfirmDeleteTeacher prompt, which handles proper verification before deletion.

    Parameters:
    - teacherId: The unique numerical ID of the teacher to delete
    Returns:
    - Success/failure status and descriptive message about the outcome")]
    public async Task<object> DeleteTeacher(
        [Description("ID of the teacher to delete")] int teacherId,
        [Description("Confirmation string. Must be 'yes' to proceed")] string confirmationToken)
    {
        if (confirmationToken != "yes")
            return new { Success = false, Message = "Підтвердження видалення не отримано. Операцію скасовано." };
    
        if (teacherId <= 0)
            return new { Success = false, Message = "Невірний ID викладача." };

        var teacher = await _ctx.Teachers
            .Include(t => t.FreeHours)
            .Include(t => t.TeacherLessons)
            .FirstOrDefaultAsync(t => t.Id == teacherId);

        if (teacher is null)
            return new { Success = false, Message = $"Викладача з ID={teacherId} не знайдено." };

        if (teacher.TeacherLessons.Any())
            return new { Success = false, Message = "Неможливо видалити: викладач призначений на один або більше уроків." };

        if (teacher.FreeHours.Any())
            _ctx.FreeHours.RemoveRange(teacher.FreeHours);

        _ctx.Teachers.Remove(teacher);
        await _ctx.SaveChangesAsync();

        return new { Success = true, Message = "Викладача успішно видалено." };
    }

}