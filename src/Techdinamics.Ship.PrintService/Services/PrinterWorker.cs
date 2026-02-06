using Microsoft.Extensions.Logging;
using Techdinamics.Ship.PrintService.Helpers;
using Techdinamics.Ship.PrintService.Models;

namespace Techdinamics.Ship.PrintService.Services;

public class PrinterWorker : IPrinterWorker
{
    private readonly ITechshipApiClient _apiClient;
    private readonly IPrintService _printService;
    private readonly ILogger<PrinterWorker> _logger;

    private DateTime? _lastSuccessfulPoll;
    private int _successCount;
    private int _failureCount;
    private string _printerName = string.Empty;

    public PrinterWorker(
        ITechshipApiClient apiClient,
        IPrintService printService,
        ILogger<PrinterWorker> logger)
    {
        _apiClient = apiClient;
        _printService = printService;
        _logger = logger;
    }

    public WorkerHealthStatus GetHealthStatus()
    {
        return new WorkerHealthStatus
        {
            PrinterName = _printerName,
            SuccessCount = _successCount,
            FailureCount = _failureCount,
            LastSuccessfulPoll = _lastSuccessfulPoll,
            // Healthy if polled successfully in the last 5 minutes (or never polled yet)
            IsHealthy = !_lastSuccessfulPoll.HasValue || (DateTime.UtcNow - _lastSuccessfulPoll.Value).TotalMinutes < 5
        };
    }

    public async Task RunAsync(PrinterConfiguration config, CancellationToken ct)
    {
        _printerName = config.Name;
        _logger.LogInformation("Worker started for printer: {Name} (Portal: {Portal}, PrinterId: {PrinterId})", 
            config.Name, config.Portal, config.PrinterId);
        
        _apiClient.Initialize(config);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await ProcessNextJobAsync(config, ct);
                _lastSuccessfulPoll = DateTime.UtcNow;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Normal shutdown
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in worker {Name} while processing jobs", config.Name);
            }

            try 
            {
                await Task.Delay(config.PollingIntervalMs, ct);
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown
            }
        }

        _logger.LogInformation("Worker stopped for printer: {Name}. Success: {SuccessCount}, Failures: {FailureCount}", 
            config.Name, _successCount, _failureCount);
    }

    private async Task ProcessNextJobAsync(PrinterConfiguration config, CancellationToken ct)
    {
        // 1. Poll for job
        var job = await _apiClient.GetNextPrintJobAsync(ct);
        if (job == null)
        {
            return;
        }

        _logger.LogInformation("Processing job {RecordId} for client {ClientName} on printer {Name}", 
            job.RecordId, job.ClientName, config.Name);

        try
        {
            await RetryHelper.ExecuteWithRetryAsync(async () =>
            {
                // 2. Print label
                if (job.LabelData != null && job.LabelData.Length > 0)
                {
                    if (string.Equals(job.LabelType, "ZPL", StringComparison.OrdinalIgnoreCase))
                    {
                        await _printService.PrintZplAsync(job.LabelData, config, CancellationToken.None);
                    }
                    else if (string.Equals(job.LabelType, "PDF", StringComparison.OrdinalIgnoreCase))
                    {
                        await _printService.PrintPdfAsync(job.LabelData, config, isThermalLabel: true, CancellationToken.None);
                    }
                    else
                    {
                        _logger.LogWarning("Unknown label type '{LabelType}' for job {RecordId}. Attempting raw print.", 
                            job.LabelType, job.RecordId);
                        await _printService.PrintRawAsync(job.LabelData, config, CancellationToken.None);
                    }
                }

                // 3. Print packing slip if exists and not skipped
                if (job.PackingSlipData != null && job.PackingSlipData.Length > 0)
                {
                    if (config.SkipPackingSlips)
                    {
                        _logger.LogInformation("Skipping packing slip for job {RecordId} as configured", job.RecordId);
                    }
                    else
                    {
                        _logger.LogInformation("Printing packing slip for job {RecordId}", job.RecordId);
                        await _printService.PrintPdfAsync(job.PackingSlipData, config, isThermalLabel: false, CancellationToken.None);
                    }
                }

                // 4. Confirm print success
                if (!string.IsNullOrEmpty(job.RecordId))
                {
                    await _apiClient.ConfirmPrintAsync(job.RecordId, CancellationToken.None);
                    _logger.LogInformation("Successfully processed and confirmed job {RecordId}", job.RecordId);
                }
            }, 
            retryCount: 3, 
            onRetry: (ex, retry) => _logger.LogWarning(ex, "Retry {Retry} for job {RecordId}", retry, job.RecordId));

            _successCount++;
        }
        catch (Exception ex)
        {
            _failureCount++;
            _logger.LogError(ex, "Failed to process job {RecordId} for printer {Name} after retries", job.RecordId, config.Name);
            // We don't confirm if it failed, so it might be retried or stay in queue depending on portal logic
        }
    }
}
