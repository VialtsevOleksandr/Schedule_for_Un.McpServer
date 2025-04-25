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

    [McpServerTool, Description("Знайти всі групи")]
    public async Task<IEnumerable<Group>> GetAllGroups()
    {
        var list = await _ctx.Groups.ToListAsync();
        return list;
    }
}