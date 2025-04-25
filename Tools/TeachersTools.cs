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

    [McpServerTool, Description("Знайти всіх викладачів")]
    public async Task<IEnumerable<Teacher>> GetAllTeachers()
    {
        var list = await _ctx.Teachers.ToListAsync();
        return list;
    }
}