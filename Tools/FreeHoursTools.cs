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

    [McpServerTool, Description(@"
    Returns a complete list of all free-hour records in the system.
    Use this tool when you need to retrieve every time slot marked as free (regardless of teacher or day).
    No input parameters are required.
    Each FreeHour entity includes:
    • Day (1 = Monday … 5 = Friday)  
    • NumberOfPair (1…4)  
    • IsFree flag (true if available)  
    • TeacherId (the owner of this free slot)
    • LessonId (the lesson associated with this free slot, if any)  
    ")]
    public async Task<IEnumerable<FreeHour>> GetAllFreeHours()
    {
        var list = await _ctx.FreeHours.ToListAsync();
        return list;
    }
    
}