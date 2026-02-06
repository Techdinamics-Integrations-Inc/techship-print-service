using System.Diagnostics;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Techdinamics.Ship.PrintService.Helpers;
using Techdinamics.Ship.PrintService.Models;

namespace Techdinamics.Ship.PrintService.Services;

public class PrintService : IPrintService
{
    private readonly ILogger<PrintService> _logger;

    public PrintService(ILogger<PrintService> logger)
    {
        _logger = logger;
    }

    public async Task PrintZplAsync(byte[] zplData, PrinterConfiguration config, CancellationToken ct = default)
    {
        _logger.LogInformation("Printing ZPL for {ConfigName} (Type: {Type})", config.Name, config.ZplConnectionType);

        switch (config.ZplConnectionType)
        {
            case PrinterConnectionType.Network:
                await PrintToNetworkAsync(zplData, config.ZplPrinterAddress, ct);
                break;
            case PrinterConnectionType.Local:
                await PrintToLocalAsync(zplData, config.ZplPrinterName, isRaw: true, ct);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(config.ZplConnectionType), "Unsupported ZPL connection type");
        }
    }

    public async Task PrintPdfAsync(byte[] pdfData, PrinterConfiguration config, bool isThermalLabel = false, CancellationToken ct = default)
    {
        var printerName = isThermalLabel ? config.ThermalPdfPrinterName : config.PdfPrinterName;
        _logger.LogInformation("Printing PDF for {ConfigName} (Type: {Type}, Printer: {Printer})", 
            config.Name, config.PdfConnectionType, printerName);

        switch (config.PdfConnectionType)
        {
            case PrinterConnectionType.Network:
                // PDF to network usually needs conversion or IPP, which is more complex.
                // For now, we'll log a warning or attempt direct TCP if it's a PDF-capable printer.
                _logger.LogWarning("Network PDF printing is not fully implemented. Attempting direct TCP send.");
                await PrintToNetworkAsync(pdfData, config.PdfPrinterAddress, ct);
                break;
            case PrinterConnectionType.Local:
                await PrintPdfLocalAsync(pdfData, printerName, ct);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(config.PdfConnectionType), "Unsupported PDF connection type");
        }
    }

    public async Task PrintRawAsync(byte[] rawData, PrinterConfiguration config, CancellationToken ct = default)
    {
        _logger.LogInformation("Printing Raw data for {ConfigName}", config.Name);
        // Defaulting to ZPL settings for raw if not specified, or just using a general approach
        if (!string.IsNullOrEmpty(config.ZplPrinterAddress) && config.ZplConnectionType == PrinterConnectionType.Network)
        {
            await PrintToNetworkAsync(rawData, config.ZplPrinterAddress, ct);
        }
        else if (!string.IsNullOrEmpty(config.ZplPrinterName) && config.ZplConnectionType == PrinterConnectionType.Local)
        {
            await PrintToLocalAsync(rawData, config.ZplPrinterName, isRaw: true, ct);
        }
        else
        {
            _logger.LogWarning("No suitable printer configuration found for Raw printing.");
        }
    }

    private async Task PrintToNetworkAsync(byte[] data, string address, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(address))
        {
            throw new ArgumentException("Printer address is required for network printing", nameof(address));
        }

        var parts = address.Split(':');
        var ip = parts[0];
        var port = parts.Length > 1 ? int.Parse(parts[1]) : 9100;

        _logger.LogDebug("Connecting to printer at {IP}:{Port}", ip, port);
        using var client = new TcpClient();
        await client.ConnectAsync(ip, port, ct);
        await using var stream = client.GetStream();
        await stream.WriteAsync(data, ct);
        await stream.FlushAsync(ct);
        _logger.LogDebug("Data sent successfully to {IP}:{Port}", ip, port);
    }

    private async Task PrintToLocalAsync(byte[] data, string printerName, bool isRaw, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(printerName))
        {
            throw new ArgumentException("Printer name is required for local printing", nameof(printerName));
        }

        _logger.LogDebug("Sending {Type} bytes to CUPS printer: {PrinterName}", isRaw ? "raw" : "pdf", printerName);
        await PrintToCups(data, printerName, isRaw);
    }

    private async Task PrintPdfLocalAsync(byte[] pdfData, string printerName, CancellationToken ct)
    {
        await PrintToLocalAsync(pdfData, printerName, isRaw: false, ct);
    }

    private async Task PrintToCups(byte[] data, string printerName, bool isRaw)
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllBytesAsync(tempFile, data);
            
            var args = isRaw ? $"-d {printerName} -o raw {tempFile}" : $"-d {printerName} {tempFile}";
            _logger.LogDebug("Executing: lp {Args}", args);
            
            using var process = Process.Start("lp", args);
            if (process != null)
            {
                await process.WaitForExitAsync();
                if (process.ExitCode != 0)
                {
                    throw new Exception($"lp command failed with exit code {process.ExitCode}");
                }
            }
            else
            {
                throw new Exception("Failed to start lp process");
            }
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}
