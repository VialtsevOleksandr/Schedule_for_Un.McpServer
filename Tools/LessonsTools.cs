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

    [McpServerTool, Description("Знайти всі пари")]
    public async Task<IEnumerable<Lesson>> GetAllLessons()
    {
        var list = await _ctx.Lessons.ToListAsync();
        return list;
    }
}