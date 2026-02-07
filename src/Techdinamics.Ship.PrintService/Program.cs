using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text;
using Techdinamics.Ship.PrintService.Helpers;
using Techdinamics.Ship.PrintService.Models;
using Techdinamics.Ship.PrintService.Services;

namespace Techdinamics.Ship.PrintService;

class Program
{
    static async Task Main(string[] args)
    {
        if (!PlatformHelper.IsLinux)
        {
            Console.WriteLine("CRITICAL ERROR: This service is designed to run exclusively on Linux (Docker).");
            throw new PlatformNotSupportedException("This service only supports Linux.");
        }

        var builder = WebApplication.CreateBuilder(args);

        builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
        builder.Configuration.AddJsonFile("config/appsettings.json", optional: true, reloadOnChange: true);
        builder.Configuration.AddEnvironmentVariables();

        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Logging.AddDebug();
        builder.Logging.AddProvider(new RotatingFileLoggerProvider("service.log"));

        // Bind configuration
        builder.Services.Configure<PrintServiceConfiguration>(
            builder.Configuration.GetSection("PrintService"));

        // HttpClient factory for API calls
        builder.Services.AddHttpClient<ITechshipApiClient, TechshipApiClient>();

        // Print Service
        builder.Services.AddSingleton<IPrintService, Services.PrintService>();

        // Worker Services
        builder.Services.AddScoped<IPrinterWorker, PrinterWorker>();
        builder.Services.AddSingleton<PrintWorkerManager>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<PrintWorkerManager>());
        builder.Services.AddHostedService<HealthMonitor>();

        var app = builder.Build();

        app.MapGet("/", async (PrintWorkerManager manager) =>
        {
            var statuses = manager.GetHealthStatuses().ToList();
            var overallHealthy = statuses.All(s => s.IsHealthy);

            var sb = new StringBuilder();
            sb.Append("<html><head><title>Techdinamics Print Service Status</title>");
            sb.Append("<style>body { font-family: sans-serif; margin: 20px; } ");
            sb.Append(".status-ok { color: green; font-weight: bold; } ");
            sb.Append(".status-error { color: red; font-weight: bold; } ");
            sb.Append("table { border-collapse: collapse; width: 100%; margin-top: 20px; } ");
            sb.Append("th, td { border: 1px solid #ddd; padding: 8px; text-align: left; } ");
            sb.Append("th { background-color: #f2f2f2; } ");
            sb.Append("pre { background: #f4f4f4; padding: 10px; border: 1px solid #ccc; max-height: 500px; overflow: auto; white-space: pre-wrap; word-wrap: break-word;} ");
            sb.Append("</style></head><body>");

            sb.Append("<h1>Print Service Status</h1>");
            sb.Append($"<p>Overall Status: <span class=\"{(overallHealthy ? "status-ok" : "status-error")}\">{(overallHealthy ? "OK" : "UNHEALTHY")}</span></p>");

            sb.Append("<h2>Workers</h2>");
            sb.Append("<table><tr><th>Printer Name</th><th>Status</th><th>Success</th><th>Failure</th><th>Last Poll</th></tr>");
            foreach (var status in statuses)
            {
                sb.Append("<tr>");
                sb.Append($"<td>{status.PrinterName}</td>");
                sb.Append($"<td><span class=\"{(status.IsHealthy ? "status-ok" : "status-error")}\">{(status.IsHealthy ? "Healthy" : "Stalled")}</span></td>");
                sb.Append($"<td>{status.SuccessCount}</td>");
                sb.Append($"<td>{status.FailureCount}</td>");
                sb.Append($"<td>{status.LastSuccessfulPoll?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Never"}</td>");
                sb.Append("</tr>");
            }
            sb.Append("</table>");

            sb.Append("<h2>Recent Logs (service.log)</h2>");
            sb.Append("<pre>");
            if (File.Exists("service.log"))
            {
                var logs = await File.ReadAllTextAsync("service.log");
                sb.Append(Microsoft.AspNetCore.Http.HttpMethods.IsGet("GET") ? System.Net.WebUtility.HtmlEncode(logs) : "");
            }
            else
            {
                sb.Append("Log file not found.");
            }
            sb.Append("</pre>");

            sb.Append("</body></html>");

            return Results.Content(sb.ToString(), "text/html");
        });

        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Techdinamics Print Service starting...");

        await app.RunAsync();
    }
}
