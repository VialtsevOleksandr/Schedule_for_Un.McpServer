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
public class FreeHoursTools
{
    private readonly ScheduleAPIContext _ctx;

    public FreeHoursTools(ScheduleAPIContext ctx)
    {
        _ctx = ctx;
    }

    [McpServerTool, Description("Знайти всі вільні години")]
    public async Task<IEnumerable<FreeHour>> GetAllFreeHours()
    {
        var list = await _ctx.FreeHours.ToListAsync();
        return list;
    }
    
}