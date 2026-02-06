using System.Threading;
using System.Threading.Tasks;
using Techdinamics.Ship.PrintService.Models;

namespace Techdinamics.Ship.PrintService.Services;

public interface IPrintService
{
    Task PrintZplAsync(byte[] zplData, PrinterConfiguration config, CancellationToken ct = default);
    Task PrintPdfAsync(byte[] pdfData, PrinterConfiguration config, bool isThermalLabel = false, CancellationToken ct = default);
    Task PrintRawAsync(byte[] rawData, PrinterConfiguration config, CancellationToken ct = default);
}
