using Techdinamics.Ship.PrintService.Models;

namespace Techdinamics.Ship.PrintService.Services;

public interface ITechshipApiClient
{
    void Initialize(PrinterConfiguration config);
    Task<PrintJobResponse?> GetNextPrintJobAsync(CancellationToken ct = default);
    Task ConfirmPrintAsync(string recordId, CancellationToken ct = default);
}
