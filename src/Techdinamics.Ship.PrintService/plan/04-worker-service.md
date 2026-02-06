# Subtask 04: Background Worker Service Implementation

## Objective
Implement the background worker service that manages multiple concurrent printer workers, each polling its own web API for print jobs and orchestrating the print workflow.

## Requirements

### 1. Create PrintWorkerManager (Main Hosted Service)
Create `Services/PrintWorkerManager.cs` extending `BackgroundService`:

```csharp
public class PrintWorkerManager : BackgroundService
{
    private readonly PrintServiceConfiguration _config;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PrintWorkerManager> _logger;

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
        using var scope = _serviceProvider.CreateScope();
        var worker = scope.ServiceProvider.GetRequiredService<IPrinterWorker>();
        await worker.RunAsync(printer, ct);
    }
}
```

### 2. Create IPrinterWorker Interface and Implementation
Create `Services/IPrinterWorker.cs` and `Services/PrinterWorker.cs`:

```csharp
public interface IPrinterWorker
{
    Task RunAsync(PrinterConfiguration config, CancellationToken ct);
}

public class PrinterWorker : IPrinterWorker
{
    private readonly ITechshipApiClient _apiClient;
    private readonly IPrintService _printService;
    private readonly ILogger<PrinterWorker> _logger;

    public async Task RunAsync(PrinterConfiguration config, CancellationToken ct)
    {
        _logger.LogInformation("Worker started for printer: {Name}", config.Name);
        
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await ProcessNextJobAsync(config, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in worker {Name}", config.Name);
            }
            await Task.Delay(config.PollingIntervalMs, ct);
        }
    }
}
```

### 3. Implement Job Processing Workflow
Based on legacy `DownloadTask` and `PrintTask`:

1. **Poll for job**: Call `GetNextPrintJobAsync(config)`
2. **If job exists**:
   - Log job details (RecordId, ClientName, printer name, etc.)
   - Determine label type (ZPL vs PDF)
   - Call appropriate print method with printer-specific settings
   - If packing slip exists, print it too
   - Call `ConfirmPrintAsync()` on success
3. **Handle errors**:
   - Log failures with printer context
   - Implement retry logic with backoff
   - Don't confirm failed prints

### 4. Implement Retry Logic
Reference legacy `Retry` class:
```csharp
public static class RetryHelper
{
    public static async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> action,
        int retryCount = 3,
        TimeSpan? retryInterval = null);
}
```

### 5. Add Health Monitoring (Per-Worker)
- Track last successful poll time per printer
- Track print success/failure counts per printer
- Expose aggregated health status for Docker health checks

### 6. Thread Safety Considerations
- Each worker has its own API client instance (scoped)
- Each worker has its own print queue
- Use `IServiceProvider.CreateScope()` for proper DI scoping
- No shared mutable state between workers

### 7. Graceful Shutdown
- Handle cancellation token properly in all workers
- Complete current print job before stopping
- Log shutdown events per worker

### 8. Register in Program.cs
```csharp
builder.Services.AddScoped<IPrinterWorker, PrinterWorker>();
builder.Services.AddHostedService<PrintWorkerManager>();
```

### 9. Health & Log Web Page
- Expose `/` or `/status` endpoint returning a simple HTML page with:
  - Overall health status (OK/UNHEALTHY)
  - Detailed per-worker stats
  - Recent logs (last 100 KB)

### 10. Log Rotation
- Limit log file size to 100 KB
- Keep a maximum of 5 log files (rotation)
- Automatically delete the oldest (6th) file when rotating

## Acceptance Criteria
- Service starts and spawns one worker per enabled printer configuration
- Each worker polls its own API at configured interval independently
- Workers run concurrently without blocking each other
- Successfully processes print jobs end-to-end per printer
- Confirms prints after successful completion
- Handles errors in one worker without affecting others
- Shuts down gracefully on container stop (all workers)
- Logs all significant events with printer context
- Health monitoring for Docker: Exposed via files `health.txt` and `health.json` by `HealthMonitor`
- Web UI: Simple health and log dashboard available via HTTP
- Log Management: Files are rotated at 100 KB, keeping only 5 most recent files
