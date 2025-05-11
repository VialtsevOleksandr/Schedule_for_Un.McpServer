using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using Schedule_for_Un.Models;
using Schedule_for_Un.McpServer.Tools;
using Schedule_for_Un.McpServer.Prompts;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

// // Забираємо всі стандартні провайдери логів
// builder.Logging.ClearProviders();

// // Додаємо провайдери логів
// builder.Logging
//        .AddConsole(opts => 
//            opts.LogToStandardErrorThreshold = LogLevel.Error)
//        .SetMinimumLevel(LogLevel.Error);

// // Логи для ModelContextProtocol
// builder.Logging.AddFilter("ModelContextProtocol", LogLevel.Warning);
// builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);

builder.Logging.AddConsole(options => {
options.LogToStandardErrorThreshold = LogLevel.Trace;
});


builder.Services.AddDbContext<ScheduleAPIContext>(opts =>
    opts.UseSqlServer(
        "Server=DESKTOP-JCD2U32\\SQLEXPRESS;Database=ScheduleForUniversity;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
    )
);

builder.Services.AddScoped<LessonsTools>();

builder.Services
       .AddMcpServer()
       .WithStdioServerTransport()
       .WithToolsFromAssembly()
       .WithPromptsFromAssembly();  

await builder.Build().RunAsync();