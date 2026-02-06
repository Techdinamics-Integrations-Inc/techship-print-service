# Subtask 03: Print Service Implementation

## Objective
Implement the print service that handles ZPL and PDF printing to configured printers. The service runs exclusively in a Linux container and supports both network and local (CUPS) printer connections. Non-Linux platforms and local file output are not supported.

## Requirements

### 1. Create IPrintService Interface
Create `Services/IPrintService.cs`:

```csharp
public interface IPrintService
{
    Task PrintZplAsync(byte[] zplData, PrinterConfiguration config, CancellationToken ct = default);
    Task PrintPdfAsync(byte[] pdfData, PrinterConfiguration config, bool isThermalLabel = false, CancellationToken ct = default);
    Task PrintRawAsync(byte[] rawData, PrinterConfiguration config, CancellationToken ct = default);
}
```

### 2. Printer Connection Types
Support two connection modes for each printer type:

**Configuration Model Update** (in `PrinterConfiguration`):
```csharp
public enum PrinterConnectionType { Network, Local }

public class PrinterConfiguration
{
    // ... existing fields ...
    
    // ZPL Printer Settings
    public PrinterConnectionType ZplConnectionType { get; set; } = PrinterConnectionType.Network;
    public string ZplPrinterAddress { get; set; }    // For Network: "IP:port" (default 9100)
    public string ZplPrinterName { get; set; }       // For Local/USB: system printer name
    
    // PDF Printer Settings  
    public PrinterConnectionType PdfConnectionType { get; set; } = PrinterConnectionType.Local;
    public string PdfPrinterAddress { get; set; }    // For Network: "IP:port"
    public string PdfPrinterName { get; set; }       // For Local: CUPS printer name
    
    // Thermal PDF printer (for labels)
    public string ThermalPdfPrinterName { get; set; }
}
```

### 3. Implement ZPL Printing

#### Network Mode (Docker-compatible)
```csharp
// Send ZPL directly via TCP socket
using var client = new TcpClient();
await client.ConnectAsync(printerIp, printerPort, ct);
await using var stream = client.GetStream();
await stream.WriteAsync(zplData, ct);
```

#### Local/USB Mode (Linux/CUPS)
Since the service runs in a Linux container, local/USB printers must be exposed via CUPS.
```csharp
// Use lp command with -o raw for ZPL
await Process.Start("lp", $"-d {config.ZplPrinterName} -o raw {tempFile}").WaitForExitAsync(ct);
```

### 4. Implement PDF Printing

#### Local Mode (Linux with CUPS)
The service runs in a Linux container, so PDF printing is handled by the `lp` command.

```csharp
// Linux - using CUPS lp command
var tempFile = Path.GetTempFileName();
await File.WriteAllBytesAsync(tempFile, pdfData, ct);
await Process.Start("lp", $"-d {config.PdfPrinterName} {tempFile}").WaitForExitAsync(ct);
```

#### Network Mode
Convert PDF to image/raw and send via TCP, or use IPP protocol.

### 5. Implement Raw Printing
For generic raw data (used when `GenericRaw` flag is set):
- Network: TCP socket to printer
- Local: CUPS lp -o raw

### 6. Acceptable Platform
The service MUST run on Linux. It will check the platform at startup and throw an exception if not on Linux.

### 8. Container-Only Strategy
The service is designed to run exclusively in a Linux container.

| Scenario | ZPL Network | ZPL Local | PDF Local | PDF Network |
|----------|-------------|-----------|-----------|-------------|
| Container (Linux) | TCP Socket | CUPS lp -o raw | CUPS lp | TCP (PDF-direct) |

### 10. (Removed) File Output Mode for Testing
Local file output is no longer supported for testing to align with container security best practices.

## Acceptance Criteria
- Can print ZPL data to network thermal printer via TCP
- Can print ZPL data to local printer via CUPS (lp -o raw)
- Can print PDF to local system printer via CUPS (lp)
- Handles connection errors gracefully with retries
- Optimized for Linux container environment
- Interface allows for easy mocking in tests
