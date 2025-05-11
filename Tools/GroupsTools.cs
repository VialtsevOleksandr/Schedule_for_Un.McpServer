using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using Schedule_for_Un.Models;
using Schedule_for_Un.McpServer.Prompts;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Schedule_for_Un.McpServer.Tools;

public class GroupResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = null!;
    public Group? Group { get; set; }
    public IEnumerable<Group> Groups { get; set; } = new List<Group>();
}

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
    
    private static readonly string[] ValidSpecialties = new[]
    {
        "Прикладна математика",
        "Системний аналіз",
        "Інформатика",
        "Програмна інженерія"
    };

    private bool IsValidSpecialty(string specialty)
    {
        return ValidSpecialties.Contains(specialty);
    }

    public async Task<List<string>> GetAvailableSpecialties()
    {
        return await _ctx.Groups.Select(g => g.Specialty).Distinct().ToListAsync();
    }


    [McpServerTool, Description(
        "Fetches all groups in the database." +
        "No input parameters required." +
        "Returns an IEnumerable<Group> with every registered group.")]
    public async Task<IEnumerable<Group>> GetAllGroups()
    {
        return await _ctx.Groups.ToListAsync();
    }

    //[McpServerTool, Description("Знайти групи за опціональними параметрами: назва, курс та спеціальність")]
    public async Task<IEnumerable<Group>> GetGroupsFiltered(string? name = null, int? course = null, string? specialty = null)
    {
        var query = _ctx.Groups.AsQueryable();
        
        if (!string.IsNullOrWhiteSpace(name))
        {
            query = query.Where(g => g.Name.Contains(name.Trim()));
        }
        
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

    // [McpServerTool, Description("Знайти групи за курсом")]
    [McpServerTool, Description(
        "Retrieves all groups for a specific course number. " +
        "Parameters: " +
        "- course (int): the course number (1–4). " +
        "Returns GroupResult.Success=false if the course is invalid or no groups found; otherwise returns Success=true and Groups list.")]
    public async Task<GroupResult> GetGroupsByCourse(
        [Description("Course number (1–4)")] int course)
    {
        if (course <= 0)
        {
            return new GroupResult { Success = false, Message = "Некоректно введено курс" };
        }
        
        var groups = await GetGroupsFiltered(course: course);
        if (!groups.Any())
        {
            return new GroupResult { 
                Success = false, 
                Message = $"Групи з курсом '{course}' не знайдено"
            };
        }
        return new GroupResult { Success = true, Groups = groups };
    }

    // [McpServerTool, Description("Знайти групи за спеціальністю")]
    // public async Task<GroupResult> GetGroupsBySpecialty(string specialty)
    [McpServerTool, Description(
        "Retrieves all groups for a given specialty. " +
        "Parameters: " +
        "- specialty (string): exact specialty name (e.g. 'Інформатика'). " +
        "Returns GroupResult.Success=false if input is empty or no matches; otherwise returns Success=true and Groups list.")]
    public async Task<GroupResult> GetGroupsBySpecialty(
        [Description("Specialty name")] string specialty)
    {
        if (string.IsNullOrWhiteSpace(specialty))
        {
            return new GroupResult { Success = false, Message = "Спеціальність не може бути порожньою" };
        }
        
        var groups = await GetGroupsFiltered(specialty: specialty);
        
        if (!groups.Any())
        {
            return new GroupResult { 
                Success = false, 
                Message = $"Групи з спеціальністю '{specialty}' не знайдено"
            };
        }
        
        return new GroupResult { Success = true, Groups = groups };
    }

    // [McpServerTool, Description(@"
    // Пошук груп за точною назвою. 
    // • Повертає перелік груп у властивості 'Groups' або помилку, якщо нічого не знайдено.
    // • Може використовуватися як самостійна tool для пошуку.
    // • Може використовуватися як перший крок у процесі видалення групи (перед викликом prompt ConfirmDeleteGroup)
    // • ВАЖЛИВО в процесах оновлення чи видалення (UpdateGroup, DeleteGroup) цей метод викликається всередині tools, тому зовнішній виклик перед ними не потрібен.")]
    // public async Task<GroupResult> GetGroupsByName(
    // [Description("Назва групи для пошуку")] string name)
    [McpServerTool, Description(
        "Searches for groups by exact or partial name match. " +
        "Parameters: " +
        "- name (string): substring of the group name to search for. " +
        "Returns GroupResult with Groups list if found; otherwise Success=false with a message.")]
    public async Task<GroupResult> GetGroupsByName(
        [Description("Name or substring of the group to find")] string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return new GroupResult { Success = false, Message = "Назва групи не може бути порожньою" };
        }
        
        var groups = await GetGroupsFiltered(name: name);
        
        if (!groups.Any())
        {
            return new GroupResult { 
                Success = false, 
                Message = $"Групи з назвою '{name}' не знайдено"
            };
        }
        
        return new GroupResult { Success = true, Groups = groups };
    }

    [McpServerTool, Description(@"
    Creates a new student group with specified parameters: 'name', 'course', 'specialty'.
    Validates:
    • name uniqueness within the database,
    • course range (1-4),
    • specialty from the allowed list: Прикладна математика, Системний аналіз, Інформатика, Програмна інженерія.
    Returns detailed error messages with guidance in case of validation failures; on success returns the created Group object.
    
    Context for LLM:
    - This tool creates university student groups that will be used in schedule management
    - Proper validation is critical to maintain data consistency")]
    public async Task<GroupResult> CreateGroup(
        [Description("Unique group name (e.g. 'К-10', 'МІ-31')")] string name,
        [Description("Course number (1-4)")] byte course,
        [Description("Specialty (must be one of the allowed values: Прикладна математика, Системний аналіз, Інформатика, Програмна інженерія)")] 
        string specialty)
    {
        if (string.IsNullOrWhiteSpace(name))
            return new GroupResult { Success = false, Message = "Назва групи не може бути порожньою." };

        if (course < 1 || course > 4)
            return new GroupResult { Success = false, Message = $"Курс має бути від 1 до 4, отримано: {course}." };

        if (string.IsNullOrWhiteSpace(specialty))
            return new GroupResult { Success = false, Message = "Спеціальність не може бути порожньою." };
        
        if (!IsValidSpecialty(specialty))
            return new GroupResult { Success = false, Message = $"Неправильна спеціальність: '{specialty}'. Допустимі: 'Прикладна математика', 'Системний аналіз', 'Інформатика', 'Програмна інженерія'." };
        
        // Перевіряємо чи існує група з таким ім'ям
        var existingGroup = await _ctx.Groups.FirstOrDefaultAsync(g => g.Name == name);
        if (existingGroup != null)
            return new GroupResult { Success = false, Message = $"Група з назвою '{name}' вже існує." };
        
        var group = new Group {
            Name      = name.Trim(),
            Course    = course,
            Specialty = specialty.Trim()
        };
        _ctx.Groups.Add(group);
        await _ctx.SaveChangesAsync();

        return new GroupResult {
            Success = true,
            Message = "Групу успішно створено.",
            Group   = group
        };
    }

    [McpServerTool,
    Description(@"
    Updates a group's information. IMPORTANT: Does not require prior GetGroupsByName call by the client.
    • The tool finds the target group using the provided 'currentName'.
    • Validates each optional field (newName, course, specialty) with clear, contextual error messages.
    • If 'specialty' is invalid, returns the complete list of allowed specialties to guide the user.
    
    Context for LLM:
    - Part of the group management workflow: find → validate → save.
    - Ensures name uniqueness (no duplicates), course within range [1-4], and specialty from the approved set.
    - All changes are optional - only provided fields will be updated.
    - Returns detailed feedback on the success or failure of the operation.")]
    public async Task<GroupResult> UpdateGroup(
        [Description("Exact current name of the group to update")] string currentName,
        [Description("Optional new name (must be unique)")] string newName = "",
        [Description("Optional new course number (1-4)")] byte course = 0,
        [Description(@"Optional new specialty.
    If invalid, error will indicate all valid options:
    " + "\"Прикладна математика\", \"Системний аналіз\", \"Інформатика\", \"Програмна інженерія\"")] 
        string specialty = "")
    {
        // Знаходимо групи за назвою
        var groups = await GetGroupsFiltered(name: currentName);
        if (!groups.Any())
            return new GroupResult { Success = false, Message = $"Групу з назвою '{currentName}' не знайдено." };

        if (groups.Count() > 1)
            return new GroupResult { Success = false, Message = $"Знайдено декілька груп з назвою '{currentName}'. Спробуйте вказати точнішу назву." };
        
        // Отримуємо єдину знайдену групу
        var group = groups.First();
        bool isChanged = false;
        
        if (!string.IsNullOrWhiteSpace(newName))
        {
            var nm = newName.Trim();
            if (nm != group.Name)
            {
                if (await _ctx.Groups.AnyAsync(g => g.Name == nm && g.Id != group.Id))
                    return new GroupResult {
                        Success = false,
                        Message = $"Група з назвою '{nm}' вже існує."
                    };
                group.Name = nm;
                isChanged = true;
            }
        }

        if (course > 0 && course != group.Course)
        {
            if (course < 1 || course > 4)
                return new GroupResult {
                    Success = false,
                    Message = $"Курс повинен бути між 1 і 4, отримано: {course}."
                };
            group.Course = course;
            isChanged = true;
        }
        
        if (!string.IsNullOrWhiteSpace(specialty) || specialty == null)
        {
            var sp = specialty.Trim();
            if (sp != group.Specialty)
            {
                if (!IsValidSpecialty(sp))
                    return new GroupResult {
                        Success = false,
                        Message = "Невідома спеціальність: '" + sp + "'. " +
                                "Допустимі спеціальності: " +
                                string.Join(", ", ValidSpecialties) + "."
                    };
                group.Specialty = sp;
                isChanged = true;
            }
        }
        
        if (!isChanged)
            return new GroupResult { Success = true, Message = "Дані групи не змінено.", Group = group };
        
        await _ctx.SaveChangesAsync();
        return new GroupResult {
            Success = true,
            Message = "Дані групи успішно оновлено.",
            Group = group
        };
    }

    [McpServerTool, Description(@"
        Deletes a group from the system by their unique ID. This tool should be used with caution and only after confirmation.
        This tool works with the ConfirmDeleteGroup prompt, which handles proper verification before deletion.

        Parameters:
        - groupId: The unique numerical ID of the group to delete
        - confirmationToken: Must be 'yes' to proceed with deletion
        Returns:
        - Success/failure status and descriptive message about the outcome")]
    public async Task<GroupResult> DeleteGroup(
        [Description("ID groups for deleting")] int groupId,
        [Description("Confirmation string. Must be 'yes' to proceed")] string confirmationToken)
    {
        if (confirmationToken != "yes")
            return new GroupResult { Success = false, Message = "Підтвердження видалення не отримано. Операцію скасовано." };
            
        if (groupId <= 0)
            return new GroupResult { Success = false, Message = "Невірний ID групи." };

        var group = await _ctx.Groups.FindAsync(groupId);
        if (group is null)
            return new GroupResult { Success = false, Message = $"Групу з ID={groupId} не знайдено." };

        bool hasLessons = await _ctx.GroupLessons.AnyAsync(gl => gl.GroupId == groupId);
        if (hasLessons)
            return new GroupResult { Success = false, Message = "Неможливо видалити: у групи є заняття." };

        _ctx.Groups.Remove(group);
        await _ctx.SaveChangesAsync();

        return new GroupResult { Success = true, Message = "Група успішно видалена." };
    }
}