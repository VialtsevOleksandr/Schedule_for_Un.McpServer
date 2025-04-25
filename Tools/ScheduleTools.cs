using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using Schedule_for_Un.Models;
// using Microsoft.Extensions.Logging;

namespace Schedule_for_Un.McpServer.Tools;

[McpServerToolType]
public class ScheduleTools
    {
        private readonly ScheduleAPIContext _ctx;
        // private readonly ILogger<ScheduleTools> _log;  // <<< ILogger

        // public ScheduleTools(ScheduleAPIContext ctx, ILogger<ScheduleTools> log)
        // {
        //     _ctx = ctx;
        //     _log = log;
        // }
        public ScheduleTools(ScheduleAPIContext ctx)
        {
            _ctx = ctx;
        }

        [McpServerTool,
         Description(
           "Знайти вільних викладачів за опційними параметрами:\n" +
           "- day: день тижня (1=понеділок … 7=неділя) або 0 для всіх днів\n" +
           "- pair: номер пари (1…n) або 0 для всіх пар"
        )]
        public async Task<IEnumerable<Teacher>> GetAvailableTeachers(int day = 0, int pair = 0)
        {
            // Створюємо запит по всіх записах
            var q = _ctx.FreeHours.AsQueryable();
            // Накладаємо фільтри лише якщо передано значення більше 0
            if (day > 0)
                q = q.Where(fh => fh.Day == day);

            if (pair > 0)
                q = q.Where(fh => fh.NumberOfPair == pair);

            var freeHours = await q.Where(fh => fh.IsFree).ToListAsync();
            var teacherIds = freeHours.Select(fh => fh.TeacherId).Distinct().ToList();

            // Завантажуємо викладачів разом з їхніми вільними годинами
            var teachers = await _ctx.Teachers
                .Where(t => teacherIds.Contains(t.Id))
                .Include(t => t.FreeHours.Where(fh => fh.IsFree))
                .ToListAsync();

            // Фільтруємо вільні години для кожного викладача за потреби
            foreach (var teacher in teachers)
            {
                if (day > 0 || pair > 0)
                {
                    var filteredHours = teacher.FreeHours.AsQueryable();
                    if (day > 0)
                        filteredHours = filteredHours.Where(fh => fh.Day == day);
                    
                    if (pair > 0)
                        filteredHours = filteredHours.Where(fh => fh.NumberOfPair == pair);
                    
                    teacher.FreeHours = filteredHours.ToList();
                }
            }

            return teachers;
        }
    }