using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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

        var host = Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                config.AddEnvironmentVariables();
            })
            .ConfigureServices((context, services) =>
            {
                // Bind configuration
                services.Configure<PrintServiceConfiguration>(
                    context.Configuration.GetSection("PrintService"));

                // HttpClient factory for API calls
                services.AddHttpClient<ITechshipApiClient, TechshipApiClient>();

                // Print Service
                services.AddSingleton<IPrintService, Services.PrintService>();

                // TODO: Register worker services here
            })
            .ConfigureLogging((context, logging) =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.AddDebug();
            })
            .Build();

        var logger = host.Services.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Techdinamics Print Service starting...");

        await host.RunAsync();
    }
}
