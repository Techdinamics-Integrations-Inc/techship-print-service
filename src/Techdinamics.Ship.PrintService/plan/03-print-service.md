# Subtask 03: Print Service Implementation

## Objective
Implement the print service that handles ZPL and PDF printing to configured printers, supporting both network and USB/local printer connections.

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
public enum PrinterConnectionType { Network, Local, File }

public class PrinterConfiguration
{
    // ... existing fields ...
    
    // ZPL Printer Settings
    public PrinterConnectionType ZplConnectionType { get; set; } = PrinterConnectionType.Network;
    public string ZplPrinterAddress { get; set; }    // For Network: "IP:port" (default 9100)
    public string ZplPrinterName { get; set; }       // For Local/USB: Windows printer name
    public string ZplOutputFile { get; set; }        // For File: path to output file (testing)
    
    // PDF Printer Settings  
    public PrinterConnectionType PdfConnectionType { get; set; } = PrinterConnectionType.Local;
    public string PdfPrinterAddress { get; set; }    // For Network: "IP:port"
    public string PdfPrinterName { get; set; }       // For Local: Windows/CUPS printer name
    public string PdfOutputFile { get; set; }        // For File: path to output file (testing)
    
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

#### Local/USB Mode (Windows)
Use raw printing via Windows spooler (similar to legacy SharpZebra):
```csharp
// Use RawPrinterHelper or P/Invoke to send raw data to printer
RawPrinterHelper.SendBytesToPrinter(printerName, zplData);
```

Reference: Legacy `PrintDirectZpl` uses `Com.SharpZebra.Printing.SpoolPrinter`

#### File Mode (Testing)
```csharp
await File.WriteAllBytesAsync(config.ZplOutputFile, zplData, ct);
// Append separator for multiple prints
await File.AppendAllTextAsync(config.ZplOutputFile + ".log", $"[{DateTime.UtcNow:O}] Printed {zplData.Length} bytes\n");
```

### 4. Implement PDF Printing

#### Local Mode (Windows/Linux with CUPS)
- Windows: Use `System.Drawing.Printing` with PdfiumViewer
- Linux: Use `lp` command with CUPS

```csharp
// Windows - using PdfiumViewer
using var document = PdfDocument.Load(new MemoryStream(pdfData));
using var printDocument = document.CreatePrintDocument();
printDocument.PrinterSettings.PrinterName = config.PdfPrinterName;
printDocument.Print();

// Linux - using CUPS lp command
var tempFile = Path.GetTempFileName();
await File.WriteAllBytesAsync(tempFile, pdfData, ct);
await Process.Start("lp", $"-d {config.PdfPrinterName} {tempFile}").WaitForExitAsync(ct);
```

#### Network Mode
Convert PDF to image/raw and send via TCP, or use IPP protocol.

#### File Mode (Testing)
```csharp
await File.WriteAllBytesAsync(config.PdfOutputFile, pdfData, ct);
```

### 5. Implement Raw Printing
For generic raw data (used when `GenericRaw` flag is set):
- Network: TCP socket to printer
- Local: RawPrinterHelper
- File: Write to file

### 6. Platform Detection
```csharp
public static class PlatformHelper
{
    public static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    public static bool IsLinux => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
    public static bool IsMacOS => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
}
```

### 7. RawPrinterHelper for Windows
Implement or use existing raw printer helper:
```csharp
public static class RawPrinterHelper
{
    [DllImport("winspool.drv", SetLastError = true)]
    public static extern bool OpenPrinter(string pPrinterName, out IntPtr phPrinter, IntPtr pDefault);
    
    [DllImport("winspool.drv", SetLastError = true)]
    public static extern bool StartDocPrinter(IntPtr hPrinter, int level, ref DOCINFO pDocInfo);
    
    // ... other P/Invoke declarations ...
    
    public static bool SendBytesToPrinter(string printerName, byte[] bytes) { /* ... */ }
}
```

### 8. Reference Legacy Implementation
From `ProcessingHostViewModel.cs`:
- `PrintDirectZpl`: Uses SharpZebra SpoolPrinter for USB printers
- `PrintDirectRaw`: Uses RawPrinterHelper for Windows raw printing
- `PrintPdf`: Uses PdfiumViewer to render and print to system printer
- `PrintImage`: Uses System.Drawing for image printing
- Supports `ThermalPdfPrinterName` for thermal label PDFs vs `GenericPrinterName` for packing slips

### 9. Cross-Platform Strategy
| Platform | ZPL Network | ZPL USB | PDF Local | PDF Network |
|----------|-------------|---------|-----------|-------------|
| Windows  | TCP Socket  | RawPrinterHelper | PdfiumViewer | TCP/Convert |
| Linux    | TCP Socket  | CUPS lp -o raw | CUPS lp | TCP/Convert |
| Docker   | TCP Socket  | N/A (use network) | CUPS | TCP/Convert |
| macOS    | TCP Socket  | CUPS lp -o raw | CUPS lp | TCP/Convert |

### 10. File Output Mode for Testing
For integration testing without physical printers:
- Configure `ZplConnectionType: File` and `ZplOutputFile: /output/zpl_output.txt`
- Configure `PdfConnectionType: File` and `PdfOutputFile: /output/pdf_output.pdf`
- Service writes to files instead of printers
- Test harness can verify file contents

## Acceptance Criteria
- Can print ZPL data to network thermal printer via TCP
- Can print ZPL data to USB/local printer on Windows
- Can print PDF to local system printer (Windows/CUPS)
- Can output to files for testing scenarios
- Handles connection errors gracefully with retries
- Works in Docker container environment (network mode)
- Works on Windows host (USB/local mode)
- Interface allows for easy mocking in tests
- Platform detection selects appropriate implementation
