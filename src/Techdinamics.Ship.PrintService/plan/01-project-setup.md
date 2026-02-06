# Subtask 01: Project Setup and Configuration Model

## Objective
Set up the .NET project structure with proper dependencies and create the configuration model for the print service.

## Requirements

### 1. Update Project File (Techdinamics.Ship.PrintService.csproj)
- Target .NET 8.0 or 9.0 (LTS preferred for Docker)
- Add required NuGet packages:
  - `Microsoft.Extensions.Hosting` - for background service hosting
  - `Microsoft.Extensions.Configuration` - for configuration
  - `Microsoft.Extensions.Configuration.EnvironmentVariables` - for Docker env vars
  - `Microsoft.Extensions.Http` - for HttpClient factory
  - `Microsoft.Extensions.Logging` - for logging
  - `System.Drawing.Common` (if needed for PDF rendering)

### 2. Create Configuration Models
Create `Models/PrinterConfiguration.cs` for individual printer config and `Models/PrintServiceConfiguration.cs` as root:

```csharp
public enum PrinterConnectionType { Network, Local, File }

// Single printer/portal configuration
public class PrinterConfiguration
{
    public string Name { get; set; }             // Friendly name for logging
    public bool Enabled { get; set; } = true;
    public string Portal { get; set; }           // e.g., "techship.example.com"
    public string Username { get; set; }
    public string Password { get; set; }
    public string ApiSecret { get; set; }        // Alternative to password
    public string ConnectionName { get; set; }   // Client key
    public string PrinterId { get; set; }        // Unique printer identifier
    
    // ZPL Printer settings
    public PrinterConnectionType ZplConnectionType { get; set; } = PrinterConnectionType.Network;
    public string ZplPrinterAddress { get; set; }  // For Network: IP:port (default 9100)
    public string ZplPrinterName { get; set; }     // For Local/USB: Windows printer name
    public string ZplOutputFile { get; set; }      // For File: output path (testing)
    
    // PDF Printer settings
    public PrinterConnectionType PdfConnectionType { get; set; } = PrinterConnectionType.Local;
    public string PdfPrinterAddress { get; set; }  // For Network: IP:port
    public string PdfPrinterName { get; set; }     // For Local: system printer name
    public string PdfOutputFile { get; set; }      // For File: output path (testing)
    public string ThermalPdfPrinterName { get; set; }  // For thermal label PDFs
    
    // Other settings
    public int PollingIntervalMs { get; set; } = 2000;
    public bool SkipPackingSlips { get; set; } = false;
}

// Root configuration with array of printers
public class PrintServiceConfiguration
{
    public List<PrinterConfiguration> Printers { get; set; } = new();
}
```

### 3. Create appsettings.json
Create configuration file with placeholder values that can be overridden by environment variables.

### 4. Update Program.cs
Set up the host builder with:
- Configuration from appsettings.json and environment variables
- Logging configuration
- Dependency injection setup
- Placeholder for future worker service registration

## Acceptance Criteria
- Project builds successfully
- Configuration can be loaded from appsettings.json
- Configuration can be overridden via environment variables (for Docker)
- Basic logging is configured
