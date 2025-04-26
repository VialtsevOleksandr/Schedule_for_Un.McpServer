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
public class LessonsTools
{
    private readonly ScheduleAPIContext _ctx;
    
    public LessonsTools(ScheduleAPIContext ctx)
    {
        _ctx = ctx;
    }

    private IEnumerable<Lesson> SortLessons(IEnumerable<Lesson> lessons)
    {
        return lessons
            .Where(l => l.Day >= 1 && l.Day <= 5 && l.NumberOfPair >= 1 && l.NumberOfPair <= 4)
            .OrderBy(l => l.Day)
            .ThenBy(l => l.NumberOfPair)
            .ToList();
    }

    [McpServerTool, Description("Знайти всі пари")]
    public async Task<IEnumerable<Lesson>> GetAllLessons()
    {
        var list = await _ctx.Lessons
            .Include(l => l.GroupLessons)
                .ThenInclude(gl => gl.Group)
            .Include(l => l.TeacherLessons)
                .ThenInclude(tl => tl.Teacher)
            .ToListAsync();
        
        return SortLessons(list);
    }

    [McpServerTool, 
    Description("Знайти пари за опційними параметрами:\n" +
    "- day: день тижня (1=понеділок … 7=неділя) або 0 для всіх днів\n" +
    "- pair: номер пари (1…n) або 0 для всіх пар")]
    public async Task<IEnumerable<Lesson>> GetLessonsFiltered(int day = 0, int pair = 0)
    {
        var query = _ctx.Lessons.AsQueryable();

        if (day > 0 && pair > 0)
        {
            query = query.Where(l => l.Day == day && l.NumberOfPair == pair);
        }
        else if (day > 0)
        {
            query = query.Where(l => l.Day == day);
        }
        else if (pair > 0)
        {
            query = query.Where(l => l.NumberOfPair == pair);
        }

        var lessons = await query
            .Include(l => l.GroupLessons)
                .ThenInclude(gl => gl.Group)
            .Include(l => l.TeacherLessons)
                .ThenInclude(tl => tl.Teacher)
            .ToListAsync();
            
        return SortLessons(lessons);
    }

    [McpServerTool, Description("Знайти пари за типом: (лекція чи практика)")]
    public async Task<IEnumerable<Lesson>> GetLessonsByType(string type)
    {
        var query = _ctx.Lessons.AsQueryable();

        if (type == "лекція")
        {
            query = query.Where(l => l.IsLecture == true);
        }
        else if (type == "практика")
        {
            query = query.Where(l => l.IsLecture == false);
        }

        var lessons = await query
            .Include(l => l.GroupLessons)
                .ThenInclude(gl => gl.Group)
            .Include(l => l.TeacherLessons)
                .ThenInclude(tl => tl.Teacher)
            .ToListAsync();
            
        return SortLessons(lessons);
    }

    [McpServerTool, Description("Знайти пари за курсом")]
    public async Task<IEnumerable<Lesson>> GetLessonsByCourse(int course)
    {
        var lessons = await GetLessonsFiltered();
        var filteredLessons = lessons.Where(l => l.GroupLessons.Any(gl => gl.Group.Course == course)).ToList();
        
        return SortLessons(filteredLessons);
    }

    [McpServerTool, Description("Знайти пари за типом тижнем (парний чи непарний)")]
    public async Task<IEnumerable<Lesson>> GetLessonsByWeek(string week)
    {
        var lessons = await GetLessonsFiltered();
        
        if (week == "парний")
        {
            lessons = lessons.Where(l => l.IsEvenWeek == true || l.IsEvenWeek == null).ToList();
        }
        else if (week == "непарний")
        {
            lessons = lessons.Where(l => l.IsEvenWeek == false || l.IsEvenWeek == null).ToList();
        }
        
        return SortLessons(lessons);
    }

    [McpServerTool, Description("Знайти пари за курсом та типом тижня")]
    public async Task<IEnumerable<Lesson>> GetLessonsByCourseAndWeek(int course, string week)
    {
        // if (course <= 0)
        // {
        //     return new { Success = false, Message = "Некоректно введено курс" };
        // }
        // if (week != "парний" && week != "непарний")
        // {
        //     return new { Success = false, Message = "Тип тижня може бути 'парний' або 'непарний'" };
        // }
        var courseFilter = await GetLessonsByCourse(course);
        if (week == "парний")
        {
            courseFilter = courseFilter.Where(l => l.IsEvenWeek == true || l.IsEvenWeek == null).ToList();
        }
        else if (week == "непарний")
        {
            courseFilter = courseFilter.Where(l => l.IsEvenWeek == false || l.IsEvenWeek == null).ToList();
        }
        // return courseFilter.Any() ? 
        //     new { Success = true, Lessons = courseFilter } : 
        //     new { Success = false, Message = $"Пари з курсом '{course}' та типом тижня '{week}' не знайдено" };
        return courseFilter;
    }

    [McpServerTool, Description("Знайти пари за групою")]
    public async Task<IEnumerable<Lesson>> GetLessonsByGroup(string groupName)
    {
        var lessons = await GetLessonsFiltered();
        var filteredLessons = lessons.Where(l => l.GroupLessons.Any(gl => gl.Group.Name == groupName)).ToList();
        
        return SortLessons(filteredLessons);
    }

    [McpServerTool, Description("Знайти пари за за групою та типом тижня")]
    public async Task<IEnumerable<Lesson>> GetLessonsByGroupAndWeek(string groupName, string week)
    {
        var lessons = await GetLessonsByGroup(groupName);
        
        if (week == "парний")
        {
            lessons = lessons.Where(l => l.IsEvenWeek == true || l.IsEvenWeek == null).ToList();
        }
        else if (week == "непарний")
        {
            lessons = lessons.Where(l => l.IsEvenWeek == false || l.IsEvenWeek == null).ToList();
        }
        
        return SortLessons(lessons);
    }

    [McpServerTool, Description("Знайти пари за викладачем")]
    public async Task<IEnumerable<Lesson>> GetLessonsByTeacher(string teacherName)
    {
        if (string.IsNullOrWhiteSpace(teacherName))
        {
            return new List<Lesson>();
        }
        
        var teacherNameLower = teacherName.ToLower().Trim();
        
        var lessons = await _ctx.Lessons
            .Include(l => l.GroupLessons)
                .ThenInclude(gl => gl.Group)
            .Include(l => l.TeacherLessons)
                .ThenInclude(tl => tl.Teacher)
            .Where(l => l.TeacherLessons.Any(tl => 
                tl.Teacher.FullName.ToLower().Contains(teacherNameLower)))
            .ToListAsync();
        
        return SortLessons(lessons);
    }
    
    [McpServerTool, Description("Знайти пари за викладачем та типом тижня")]
    public async Task<IEnumerable<Lesson>> GetLessonsByTeacherAndWeek(string teacherName, string week)
    {
        var lessons = await GetLessonsByTeacher(teacherName);
        
        if (week == "парний")
        {
            lessons = lessons.Where(l => l.IsEvenWeek == true || l.IsEvenWeek == null).ToList();
        }
        else if (week == "непарний")
        {
            lessons = lessons.Where(l => l.IsEvenWeek == false || l.IsEvenWeek == null).ToList();
        }
        
        return SortLessons(lessons);
    }
}