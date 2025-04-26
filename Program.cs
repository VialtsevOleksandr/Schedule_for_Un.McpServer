using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using Schedule_for_Un.Models;
using Schedule_for_Un.McpServer.Tools;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDbContext<ScheduleAPIContext>(opts =>
    opts.UseSqlServer(
        "Server=DESKTOP-JCD2U32\\SQLEXPRESS;Database=ScheduleForUniversity;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
    )
);

builder.Services.AddScoped<LessonsTools>();

builder.Services
       .AddMcpServer()
       .WithStdioServerTransport()
       .WithToolsFromAssembly();

await builder.Build().RunAsync();
