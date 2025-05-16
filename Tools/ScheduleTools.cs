using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using Schedule_for_Un.Models;

namespace Schedule_for_Un.McpServer.Tools;

[McpServerToolType]
public class ScheduleTools
{
    private readonly ScheduleAPIContext _ctx;
    private readonly LessonsTools _lessonsTools;
    
    // Фіксований понеділок парного тижня
    private static readonly DateTime ReferenceMonday = new DateTime(2025, 4, 21);
    
    private readonly string[] _daysOfWeek = { "Понеділок", "Вівторок", "Середа", "Четвер", "П'ятниця" };
    private readonly string[] _pairTimes = { 
        "8:40-10:15",  // 1 пара
        "10:35-12:10", // 2 пара
        "12:20-13:55", // 3 пара
        "14:05-15:40"  // 4 пара
    };
    private static DateTime GetMondayOfWeek(DateTime date)
    {
        // C# DayOfWeek: Sunday=0, Monday=1, ..., Saturday=6
        int dow = (int)date.DayOfWeek;
        // Якщо неділя (0), віднімаємо 6 днів, інакше віднімаємо (dow-1)
        int delta = (dow == 0) ? 6 : dow - 1;
        return date.Date.AddDays(-delta);
    }
    private bool IsEvenWeek(DateTime date)
    {
        var monday = GetMondayOfWeek(date);
        // Різниця в днях
        var daysDiff = (monday - ReferenceMonday).TotalDays;
        // Цілі тижні
        var weeksDiff = (int)(daysDiff / 7);
        // Якщо змін тижнів парна — поточний тиждень того самого парного/непарного типу
        return weeksDiff % 2 == 0;
    }
    
    public ScheduleTools(ScheduleAPIContext ctx, LessonsTools lessonsTools)
    {
        _ctx = ctx;
        _lessonsTools = lessonsTools;
    }

    [McpServerTool, Description(@"
    Exports a student group's weekly schedule as a CSV string ready for Google Calendar import.
    Simply supply:
    - groupName (string): the exact name of the group
    - startDate (string, optional): any date (YYYY-MM-DD) within the desired week (defaults to today)

    Returns either:
    • A CSV-formatted string of all lessons that week, or  
    • An error message (e.g. 'Group not found' or 'Invalid date format').
    ")]
    public async Task<string> GetScheduleForGoogleCalendarByGroup(
        [Description("Name of the group")] string groupName,
        [Description("Optional start date in 'YYYY-MM-DD' format; defaults to today")] string? startDate = null
    )
    {
        if (string.IsNullOrWhiteSpace(groupName))
            return "Назва групи не може бути порожньою";

        DateTime date = DateTime.Now;
        if (!string.IsNullOrWhiteSpace(startDate))
        {
            if (DateTime.TryParse(startDate, out DateTime parsedDate))
            {
                date = parsedDate;
            }
            else
            {
                return $"Невірний формат дати: {startDate}. Використовуйте формат YYYY-MM-DD.";
            }
        }

        bool isEven = IsEvenWeek(date);
        string weekType = isEven ? "парний" : "непарний";

        var lessons = await _lessonsTools.GetLessonsByGroupAndWeek(groupName, weekType);
        if (!lessons.Any())
            return $"Пари для групи {groupName} на {weekType} тиждень не знайдено";

        DateTime mondayOfWeek = GetMondayOfWeek(date);
        
        var sb = new StringBuilder();
        sb.AppendLine("Subject,Start Date,Start Time,End Date,End Time,All Day,Description,Location");

        foreach (var l in lessons)
        {
            int d = l.Day, p = l.NumberOfPair;
            if (d < 1 || d > 5 || p < 1 || p > 4) continue;

            DateTime lessonDate = GetMondayOfWeek(date).AddDays(d - 1);
            var times = _pairTimes[p-1].Split('-');
            string subject = l.Subject.Replace("\"", "\"\"");
            
            string desc = "Викладачі: " +
                        string.Join(", ", l.TeacherLessons
                            .Select(tl => $"{tl.Teacher.FullName} ({tl.Teacher.Position})"))
                            .Replace("\"", "\"\"");
            
            var sharedGroups = l.GroupLessons
                .Where(gl => gl.Group != null && gl.Group.Name != groupName)
                .Select(gl => gl.Group.Name);
            
            if (sharedGroups.Any())
            {
                desc += $"\nРазом з групами: {string.Join(", ", sharedGroups)}";
            }

            sb.AppendLine($"\"{subject} ({(l.IsLecture?"Лекція":"Практика")})\"," +
                        $"{lessonDate:yyyy-MM-dd},{times[0]},{lessonDate:yyyy-MM-dd},{times[1]}," +
                        $"FALSE,\"{desc}\",\"ФКНК КНУ\"");
        }

        return sb.ToString();
    }


    [McpServerTool, Description(@"
    Determines whether the current calendar week is even or odd, based on the institution's alternating-week academic schedule.
    Use this tool when you need to display or make decisions according to the parity of the current week (e.g., selecting which set of lessons to show).
    No input parameters are required.
    Returns:
    - A human-readable message: 'It is an even week.' or 'It is an odd week.'")]
    public string CheckCurrentWeek()
    {
        bool isEven = IsEvenWeek(DateTime.Now);
        return isEven ? "Зараз парний тиждень" : "Зараз непарний тиждень";
    }
    
    [McpServerTool, Description(@"
    Determines whether the week containing a specified date is even or odd, according to the alternating-week academic schedule.
    Use this tool when you need to check week parity for planning or querying in advance (for example, when displaying a schedule for a future date).
    Validation:
    • The input must be a non-empty string in ISO format (YYYY-MM-DD).
    • If the date cannot be parsed, an error message is returned.
    Parameters:
    - date (string): The date to check, in 'YYYY-MM-DD' format.
    Returns:
    - On success: 'The week containing {date} is even.' or 'The week containing {date} is odd.'
    - On error: descriptive message about the invalid or missing date.
    ")]
    public string CheckWeekByDate(string date)
    {
        if (string.IsNullOrWhiteSpace(date))
        {
            return "Дата не може бути порожньою. Використовуйте формат YYYY-MM-DD.";
        }
        
        if (DateTime.TryParse(date, out DateTime parsedDate))
        {
            bool isEven = IsEvenWeek(parsedDate);
            return isEven 
                ? $"Тиждень, що містить дату {parsedDate:dd.MM.yyyy}, буде парним" 
                : $"Тиждень, що містить дату {parsedDate:dd.MM.yyyy}, буде непарним";
        }
        else
        {
            return $"Невірний формат дати: {date}. Використовуйте формат YYYY-MM-DD.";
        }
    }
    
    [McpServerTool, Description(@"
    Exports a teacher's weekly timetable as a CSV string, ready for import into Google Calendar.
    Simply provide:
    - teacherName (string): the exact full name of the instructor
    - startDate   (string, optional): any date in 'YYYY-MM-DD' format within the target week (defaults to today)
    Returns:
    - A CSV-formatted string of all lessons for that week, or  
    - An error message if the teacher is not found, the date is invalid, or no lessons exist.
    ")]
    public async Task<string> GetScheduleForGoogleCalendarByTeacher(
        [Description("Name of the teacher")] string teacherName,
        [Description("Optional start date in 'YYYY-MM-DD' format; defaults to today")] string? startDate = null
    )
    {
        if (string.IsNullOrWhiteSpace(teacherName))
            return "Назва викладача не може бути порожньою";

        DateTime date = DateTime.Now;
        if (!string.IsNullOrWhiteSpace(startDate))
        {
            if (DateTime.TryParse(startDate, out DateTime parsedDate))
            {
                date = parsedDate;
            }
            else
            {
                return $"Невірний формат дати: {startDate}. Використовуйте формат YYYY-MM-DD.";
            }
        }

        bool isEven = IsEvenWeek(date);
        string weekType = isEven ? "парний" : "непарний";

        var lessons = await _lessonsTools.GetLessonsByTeacherAndWeek(teacherName, weekType);
        if (!lessons.Any())
            return $"Пари для викладача {teacherName} на {weekType} тиждень не знайдено";

        DateTime mondayOfWeek = GetMondayOfWeek(date);
        
        var sb = new StringBuilder();
        sb.AppendLine("Subject,Start Date,Start Time,End Date,End Time,All Day,Description,Location");

        foreach (var l in lessons)
        {
            int d = l.Day, p = l.NumberOfPair;
            if (d < 1 || d > 5 || p < 1 || p > 4) continue;

            DateTime lessonDate = GetMondayOfWeek(date).AddDays(d - 1);
            var times = _pairTimes[p-1].Split('-');
            string subject = l.Subject.Replace("\"", "\"\"");
            
            var groupsByCourseAndSpecialty = l.GroupLessons
                .Where(gl => gl.Group != null)
                .GroupBy(gl => new { gl.Group.Course, gl.Group.Specialty })
                .OrderBy(g => g.Key.Course);

            var groupDescriptions = new List<string>();
            foreach (var group in groupsByCourseAndSpecialty)
            {
                string groupsList = string.Join(", ", group.Select(gl => gl.Group.Name));
                groupDescriptions.Add($"Групи {group.Key.Course} курсу спеціальності {group.Key.Specialty}: {groupsList}");
            }

            string desc = string.Join("\n", groupDescriptions).Replace("\"", "\"\"");
            
            sb.AppendLine($"\"{subject} ({(l.IsLecture?"Лекція":"Практика")})\"," +
                        $"{lessonDate:yyyy-MM-dd},{times[0]},{lessonDate:yyyy-MM-dd},{times[1]}," +
                        $"FALSE,\"{desc}\",\"ФКНК КНУ\"");
        }

        return sb.ToString();
    }
    
}