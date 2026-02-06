using Techdinamics.Ship.PrintService.Models;

namespace Techdinamics.Ship.PrintService.Services;

public interface IPrinterWorker
{
    Task RunAsync(PrinterConfiguration config, CancellationToken ct);
    WorkerHealthStatus GetHealthStatus();
}
