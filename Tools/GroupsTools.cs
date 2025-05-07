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


    [McpServerTool, Description("Знайти всі групи")]
    public async Task<IEnumerable<Group>> GetAllGroups()
    {
        var list = await _ctx.Groups.ToListAsync();
        return list;
    }

    [McpServerTool, Description("Знайти групи за опціональними параметрами: назва, курс та спеціальність")]
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

    [McpServerTool, Description("Знайти групи за курсом")]
    public async Task<GroupResult> GetGroupsByCourse(int course)
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

    [McpServerTool, Description("Знайти групи за спеціальністю")]
    public async Task<GroupResult> GetGroupsBySpecialty(string specialty)
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

    [McpServerTool, Description(@"
    Пошук груп за точною назвою. 
    • Повертає перелік груп у властивості 'Groups' або помилку, якщо нічого не знайдено.
    • Може використовуватися як самостійна tool для пошуку.
    • Може використовуватися як перший крок у процесі видалення групи (перед викликом prompt ConfirmDeleteGroup)
    • ВАЖЛИВО в процесах оновлення чи видалення (UpdateGroup, DeleteGroup) цей метод викликається всередині tools, тому зовнішній виклик перед ними не потрібен.")]
    public async Task<GroupResult> GetGroupsByName(
    [Description("Назва групи для пошуку")] string name)
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
    Створює нову групу з вказаними параметрами: 'name', 'course', 'specialty'. 
    Перевіряє:
    • унікальність назви,
    • діапазон курсу (1–4),
    • спеціальність зі списку: Прикладна математика, Системний аналіз, Інформатика, Програмна інженерія.
    У разі помилки повертає зрозуміле повідомлення з переліком допустимих значень; у разі успіху — створений об’єкт Group.")]
    public async Task<GroupResult> CreateGroup(
        [Description("Унікальна назва групи")] string name,
        [Description("Номер курсу (1–4)")] byte course,
        [Description("Спеціальність (Обов’язково одна з: Прикладна математика, Системний аналіз, Інформатика, Програмна інженерія)")] 
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
    Оновлює дані групи. ВАЖЛИВО без необхідності попереднього виклику GetGroupsByName клієнтом.
    • Інструмент знаходить цільову групу за вказаною 'currentName'.
    • Перевіряє кожне опціональне поле (newName, course, specialty) з чіткими, контекстуальними повідомленнями про помилки.
    • Якщо 'specialty' є неіснуючою, повертає повний список дозволених спеціальностей для орієнтації користувача.
    Контекст для LLM:
    - Частина робочого процесу управління групами: знайти → перевірити → зберегти.
    - Забезпечує унікальність імен (без дублікатів), курс у межах [1–4] та спеціальність із дозволеного набору.
    - Запобігає випадковій втраті даних, застосовуючи суворі перевірки перед збереженням змін.")]
    public async Task<GroupResult> UpdateGroup(
        [Description("Точна поточна назва групи для оновлення")] string currentName,
        [Description("Опціональна нова назва (має бути унікальною)")] string? newName = null,
        [Description("Опціональний новий номер курсу (1–4)")] byte course = 0,
        [Description(@"Опціональна нова спеціальність.
    Якщо недійсна, помилка вкаже всі допустимі варіанти: 
    " + "\"Прикладна математика\", \"Системний аналіз\", \"Інформатика\", \"Програмна інженерія\"")] 
        string? specialty = null)
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

    [McpServerTool, Description("Видалити групу за ID. ВАЖЛИВО: Перед викликом цього інструменту потрібно: 1) Знайти групу за допомогою GetGroupsByName; 2) Отримати підтвердження через промпт ConfirmDeleteGroup; 3) Тільки після підтвердження виконати цей виклик з ID групи")]
    public async Task<GroupResult> DeleteGroup(
        [Description("ID групи для видалення")] int groupId)
    {
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