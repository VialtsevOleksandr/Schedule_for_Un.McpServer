using System;
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
public class GroupsTools
{
    private readonly ScheduleAPIContext _ctx;

    public GroupsTools(ScheduleAPIContext ctx)
    {
        _ctx = ctx;
    }    
    private bool IsValidCourse(int course)
    {
        return course >= 1 && course <= 4;
    }
    
    private bool IsValidSpecialty(string specialty)
    {
        var validSpecialties = new[]
        {
            "Прикладна математика", 
            "Системний аналіз", 
            "Інформатика", 
            "Програмна інженерія"
        };
        
        return validSpecialties.Contains(specialty);
    }
    
    public async Task<List<string>> GetAvailableSpecialties()
    {
        return await _ctx.Groups.Select(g => g.Specialty).Distinct().ToListAsync();
    }

    [McpServerTool, Description("Знайти всі групи")]
    public async Task<IEnumerable<Group>> GetAllGroups()
    {
        var list = await _ctx.Groups.ToListAsync();
        return list;
    }

    [McpServerTool, Description("Знайти групи за опціональними параметрами курс та спеціальність")]
    public async Task<IEnumerable<Group>> GetGroupsFiltered(int? course = null, string? specialty = null)
    {
        var query = _ctx.Groups.AsQueryable();
        
        if (course.HasValue && course.Value > 0)
        {
            query = query.Where(g => g.Course == course.Value);
        }

        if (!string.IsNullOrWhiteSpace(specialty))
        {
            query = query.Where(g => g.Specialty.Contains(specialty));
        }

        return await query.ToListAsync();
    }

    [McpServerTool, Description("Знайти групи за курсом")]
    public async Task<object> GetGroupsByCourse(int course)
    {
        if (course <= 0)
        {
            return new { Success = false, Message = "Некоректно введено курс" };
        }
        
        var groups = await GetGroupsFiltered(course: course);
        if (!groups.Any())
        {
            return new { 
                Success = false, 
                Message = $"Групи з курсом '{course}' не знайдено"
            };
        }
        return new { Success = true, Groups = groups };
    }

    [McpServerTool, Description("Знайти групи за спеціальністю")]
    public async Task<object> GetGroupsBySpecialty(string specialty)
    {
        if (string.IsNullOrWhiteSpace(specialty))
        {
            return new { Success = false, Message = "Спеціальність не може бути порожньою", AvailableSpecialties = await GetAvailableSpecialties() };
        }
        
        var groups = await GetGroupsFiltered(specialty: specialty);
        
        if (!groups.Any())
        {
            return new { 
                Success = false, 
                Message = $"Групи з спеціальністю '{specialty}' не знайдено"
            };
        }
        
        return new { Success = true, Groups = groups };
    }
}