using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Techdinamics.Ship.PrintService.Services;

public class HealthMonitor : BackgroundService
{
    private readonly PrintWorkerManager _manager;
    private readonly ILogger<HealthMonitor> _logger;
    private readonly string _healthTextPath = "health.txt";
    private readonly string _healthJsonPath = "health.json";

    public HealthMonitor(PrintWorkerManager manager, ILogger<HealthMonitor> logger)
    {
        _manager = manager;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("HealthMonitor started");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var statuses = _manager.GetHealthStatuses().ToList();
                var overallHealthy = statuses.All(s => s.IsHealthy);

                var json = JsonSerializer.Serialize(new { overallHealthy, statuses });
                await File.WriteAllTextAsync(_healthJsonPath, json, stoppingToken);
                await File.WriteAllTextAsync(_healthTextPath, overallHealthy ? "OK" : "UNHEALTHY", stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // normal shutdown
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "HealthMonitor error");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // normal shutdown
            }
        }
        _logger.LogInformation("HealthMonitor stopped");
    }
}
