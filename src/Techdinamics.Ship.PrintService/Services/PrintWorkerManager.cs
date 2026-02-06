using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Techdinamics.Ship.PrintService.Models;

namespace Techdinamics.Ship.PrintService.Services;

public class PrintWorkerManager : BackgroundService
{
    private readonly PrintServiceConfiguration _config;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PrintWorkerManager> _logger;
    private readonly ConcurrentDictionary<string, IPrinterWorker> _activeWorkers = new();

    public PrintWorkerManager(
        IOptions<PrintServiceConfiguration> config,
        IServiceProvider serviceProvider,
        ILogger<PrintWorkerManager> logger)
    {
        _config = config.Value;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public IEnumerable<WorkerHealthStatus> GetHealthStatuses()
    {
        return _activeWorkers.Values.Select(w => w.GetHealthStatus());
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var enabledPrinters = _config.Printers.Where(p => p.Enabled).ToList();
        _logger.LogInformation("Starting {Count} printer workers", enabledPrinters.Count);

        var tasks = enabledPrinters.Select(printer => 
            RunPrinterWorkerAsync(printer, stoppingToken));

        await Task.WhenAll(tasks);
    }

    private async Task RunPrinterWorkerAsync(PrinterConfiguration printer, CancellationToken ct)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var worker = scope.ServiceProvider.GetRequiredService<IPrinterWorker>();
            
            if (!_activeWorkers.TryAdd(printer.Name, worker))
            {
                _logger.LogWarning("Worker for printer {Name} already exists", printer.Name);
            }

            try
            {
                await worker.RunAsync(printer, ct);
            }
            finally
            {
                _activeWorkers.TryRemove(printer.Name, out _);
            }
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Fatal error in worker manager for printer: {Name}", printer.Name);
        }
    }
}
