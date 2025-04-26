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
            6 => "Субота",
            7 => "Неділя",
            _ => "Невідомий день"
        };
    }

    [McpServerTool, Description("Знайти всіх викладачів")]
    public async Task<IEnumerable<Teacher>> GetAllTeachers()
    {
        var list = await _ctx.Teachers.ToListAsync();
        return list;
    }

    [McpServerTool,
     Description(
         "Знайти вільних викладачів за опційними параметрами:\n" +
         "- day: день тижня (1=понеділок … 7=неділя) або 0 для всіх днів\n" +
         "- pair: номер пари (1…n) або 0 для всіх пар"
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

    [McpServerTool, Description("Знайти всі вільні години викладача за ПІБ або прізвищем")]
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
    
    [McpServerTool, Description("Знайти викладача за ПІБ або прізвищем")]
    public async Task<IEnumerable<Teacher>> GetTeacherByName(string teacherName)
    {
        var teacherNameLower = teacherName.ToLower().Trim();
        var matchingTeachers = await _ctx.Teachers
            .Where(t => t.FullName.ToLower().Contains(teacherNameLower))
            .ToListAsync();

        return matchingTeachers;
    }
}