namespace Techdinamics.Ship.PrintService.Models;

public enum PrinterConnectionType { Network, Local }

/// <summary>
/// Single printer/portal configuration
/// </summary>
public class PrinterConfiguration
{
    public bool Enabled { get; set; } = true;
    public string Portal { get; set; } = string.Empty;           // e.g., "techship.example.com"
    public string ApiSecret { get; set; } = string.Empty;        // Primary auth
    public string ConnectionName { get; set; } = string.Empty;   // Client key
    
    // ZPL Printer settings
    public PrinterConnectionType ZplConnectionType { get; set; } = PrinterConnectionType.Network;
    public string ZplPrinterAddress { get; set; } = string.Empty;  // For Network: IP:port (default 9100)
    public string ZplPrinterName { get; set; } = string.Empty;     // For Local/USB: system printer name
    
    // PDF Printer settings
    public PrinterConnectionType PdfConnectionType { get; set; } = PrinterConnectionType.Local;
    public string PdfPrinterAddress { get; set; } = string.Empty;  // For Network: IP:port
    public string PdfPrinterName { get; set; } = string.Empty;     // For Local: system printer name
    public string ThermalPdfPrinterName { get; set; } = string.Empty;  // For thermal label PDFs
    
    // Other settings
    public int PollingIntervalMs { get; set; } = 2000;
    public bool SkipPackingSlips { get; set; } = false;
}
