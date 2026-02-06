# Subtask 08: Integration Testing with Mock Web Service

## Objective
Create a mock web service and integration test environment to test the print service end-to-end in Docker, outputting to files instead of physical printers.

## Requirements

### 1. Create Mock Web Service Project
Create `Techdinamics.Ship.PrintService.MockServer` ASP.NET Core minimal API project:

```bash
dotnet new web -n Techdinamics.Ship.PrintService.MockServer
dotnet sln add Techdinamics.Ship.PrintService.MockServer
```

### 2. Mock Server Features

#### 2.1 Print Job Queue with State Tracking
```csharp
public class PrintJob
{
    public string OrderId { get; set; }
    public string BatchNumber { get; set; }
    public string ClientName { get; set; }
    public string CarrierName { get; set; }
    public List<PrintLabel> Labels { get; set; } = new();
    public bool IsPrinted { get; set; }
    public DateTime? PrintedAt { get; set; }
}

public class PrintLabel
{
    public string Type { get; set; }      // "DIRECT" (ZPL), "PDF", "IMAGE"
    public string Purpose { get; set; }   // "LABEL", "PACKINGSLIP", "COMMERCIALINVOICE", "DGDECLARATION"
    public byte[] Data { get; set; }
}

public class PrintJobStore
{
    private readonly ConcurrentDictionary<string, PrintJob> _jobs = new();
    private readonly string _logFile;
    
    public PrintJob GetNextUnprinted(string clientKey, string printerId) { /* ... */ }
    public void MarkAsPrinted(string orderId) { /* ... */ }
    public void LogPrintAction(string orderId, string action) { /* ... */ }
}
```

#### 2.2 API Endpoints (matching legacy format)
```csharp
app.MapPost("/Integration/ProcessNextOrderExt", async (HttpContext ctx, PrintJobStore store) =>
{
    // Parse form data: username, password
    // Check x-secret-key header for API secret
    // Return XML response matching legacy format
    var clientKey = ctx.Request.Query["clientKey"];
    var printerId = ctx.Request.Query["uid"];
    
    var job = store.GetNextUnprinted(clientKey, printerId);
    if (job == null)
        return Results.Content("<Response></Response>", "application/xml");
    
    // Return XML with order and labels (base64 encoded)
    return Results.Content(BuildOrderXml(job), "application/xml");
});

app.MapPost("/Integration/ConfirmPrint", async (HttpContext ctx, PrintJobStore store) =>
{
    var orderId = ctx.Request.Query["orderId"];
    store.MarkAsPrinted(orderId);
    store.LogPrintAction(orderId, "CONFIRMED");
    return Results.Ok();
});
```

#### 2.3 Sample Data Endpoint (for test setup)
```csharp
app.MapPost("/api/test/add-job", async (PrintJob job, PrintJobStore store) =>
{
    store.AddJob(job);
    return Results.Ok(job.OrderId);
});

app.MapGet("/api/test/jobs", (PrintJobStore store) => store.GetAllJobs());
app.MapGet("/api/test/printed-log", (PrintJobStore store) => store.GetPrintLog());
app.MapPost("/api/test/reset", (PrintJobStore store) => { store.Reset(); return Results.Ok(); });
```

### 3. Sample Test Data Files
Create `TestData/` folder with sample documents:

#### 3.1 ZPL Labels (`sample_label.zpl`)
```zpl
^XA
^FO50,50^ADN,36,20^FDOrder: {OrderId}^FS
^FO50,100^ADN,36,20^FDShip To: {ShipToName}^FS
^FO50,150^BY3^BCN,100,Y,N,N^FD{TrackingNumber}^FS
^XZ
```

#### 3.2 PDF Files
- `sample_label.pdf` - 4x6 thermal label PDF
- `sample_packingslip.pdf` - 8.5x11 packing slip PDF
- `sample_commercial_invoice.pdf` - Commercial invoice PDF
- `sample_dg_declaration.pdf` - Dangerous goods declaration PDF

#### 3.3 Mixed Document Orders
Create test scenarios with various document combinations:
```json
{
  "testScenarios": [
    {
      "name": "ZPL Label Only",
      "labels": [{ "type": "DIRECT", "purpose": "LABEL", "file": "sample_label.zpl" }]
    },
    {
      "name": "PDF Label Only", 
      "labels": [{ "type": "PDF", "purpose": "LABEL", "file": "sample_label.pdf" }]
    },
    {
      "name": "ZPL Label + PDF Packing Slip",
      "labels": [
        { "type": "DIRECT", "purpose": "LABEL", "file": "sample_label.zpl" },
        { "type": "PDF", "purpose": "PACKINGSLIP", "file": "sample_packingslip.pdf" }
      ]
    },
    {
      "name": "Full International Order",
      "labels": [
        { "type": "DIRECT", "purpose": "LABEL", "file": "sample_label.zpl" },
        { "type": "PDF", "purpose": "PACKINGSLIP", "file": "sample_packingslip.pdf" },
        { "type": "PDF", "purpose": "COMMERCIALINVOICE", "file": "sample_commercial_invoice.pdf" },
        { "type": "PDF", "purpose": "DGDECLARATION", "file": "sample_dg_declaration.pdf" }
      ]
    },
    {
      "name": "PDF Labels + PDF Slip",
      "labels": [
        { "type": "PDF", "purpose": "LABEL", "file": "sample_label.pdf" },
        { "type": "PDF", "purpose": "PACKINGSLIP", "file": "sample_packingslip.pdf" }
      ]
    }
  ]
}
```

### 4. Docker Compose for Integration Testing
Create `docker-compose.integration.yml`:

```yaml
version: '3.8'
services:
  mock-server:
    build:
      context: ./Techdinamics.Ship.PrintService.MockServer
    ports:
      - "5080:8080"
    volumes:
      - ./TestData:/app/TestData:ro
      - ./output/server-log:/app/logs
    environment:
      - ASPNETCORE_URLS=http://+:8080

  print-service:
    build:
      context: ./Techdinamics.Ship.PrintService
    depends_on:
      - mock-server
    volumes:
      - ./output/printer:/app/output
      - ./config/test-appsettings.json:/app/appsettings.json:ro
    environment:
      - PrintService__Printers__0__Name=TestPrinter
      - PrintService__Printers__0__Portal=mock-server:8080
      - PrintService__Printers__0__Username=testuser
      - PrintService__Printers__0__Password=testpass
      - PrintService__Printers__0__ConnectionName=test-client
      - PrintService__Printers__0__PrinterId=test-printer-001
      - PrintService__Printers__0__ZplConnectionType=File
      - PrintService__Printers__0__ZplOutputFile=/app/output/zpl_output.txt
      - PrintService__Printers__0__PdfConnectionType=File
      - PrintService__Printers__0__PdfOutputFile=/app/output/pdf_output.pdf

volumes:
  output:
```

### 5. Integration Test Script
Create `run-integration-test.ps1` (PowerShell for Windows):

```powershell
# Integration Test Script for Print Service
param(
    [switch]$Cleanup,
    [int]$WaitSeconds = 30
)

$ErrorActionPreference = "Stop"
$OutputDir = "./output"

Write-Host "=== Print Service Integration Test ===" -ForegroundColor Cyan

# Cleanup previous run
if (Test-Path $OutputDir) {
    Remove-Item -Recurse -Force $OutputDir
}
New-Item -ItemType Directory -Path "$OutputDir/printer" -Force | Out-Null
New-Item -ItemType Directory -Path "$OutputDir/server-log" -Force | Out-Null

# Start services
Write-Host "`n1. Starting Docker services..." -ForegroundColor Yellow
docker-compose -f docker-compose.integration.yml up -d --build

# Wait for services to be ready
Write-Host "`n2. Waiting for services to start..." -ForegroundColor Yellow
Start-Sleep -Seconds 10

# Add test jobs via mock server API
Write-Host "`n3. Adding test print jobs..." -ForegroundColor Yellow

$testJobs = @(
    @{
        OrderId = "ORD-001"
        BatchNumber = "BATCH-2024-001"
        ClientName = "Test Client"
        CarrierName = "FedEx"
        Labels = @(
            @{ Type = "DIRECT"; Purpose = "LABEL"; Data = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes("^XA^FO50,50^ADN,36,20^FDOrder: ORD-001^FS^XZ")) }
        )
    },
    @{
        OrderId = "ORD-002"
        BatchNumber = "BATCH-2024-001"
        ClientName = "Test Client"
        CarrierName = "UPS"
        Labels = @(
            @{ Type = "DIRECT"; Purpose = "LABEL"; Data = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes("^XA^FO50,50^ADN,36,20^FDOrder: ORD-002^FS^XZ")) },
            @{ Type = "PDF"; Purpose = "PACKINGSLIP"; Data = [Convert]::ToBase64String([System.IO.File]::ReadAllBytes("./TestData/sample_packingslip.pdf")) }
        )
    }
)

foreach ($job in $testJobs) {
    $body = $job | ConvertTo-Json -Depth 5
    Invoke-RestMethod -Uri "http://localhost:5080/api/test/add-job" -Method Post -Body $body -ContentType "application/json"
    Write-Host "  Added job: $($job.OrderId)" -ForegroundColor Green
}

# Wait for print service to process
Write-Host "`n4. Waiting $WaitSeconds seconds for print service to process jobs..." -ForegroundColor Yellow
Start-Sleep -Seconds $WaitSeconds

# Check results
Write-Host "`n5. Checking results..." -ForegroundColor Yellow

# Check ZPL output
if (Test-Path "$OutputDir/printer/zpl_output.txt") {
    Write-Host "  ZPL Output:" -ForegroundColor Green
    Get-Content "$OutputDir/printer/zpl_output.txt" | ForEach-Object { Write-Host "    $_" }
} else {
    Write-Host "  WARNING: No ZPL output file found" -ForegroundColor Red
}

# Check PDF output
if (Test-Path "$OutputDir/printer/pdf_output.pdf") {
    $pdfSize = (Get-Item "$OutputDir/printer/pdf_output.pdf").Length
    Write-Host "  PDF Output: $pdfSize bytes" -ForegroundColor Green
} else {
    Write-Host "  WARNING: No PDF output file found" -ForegroundColor Red
}

# Check server print log
Write-Host "`n6. Server print log:" -ForegroundColor Yellow
$printedJobs = Invoke-RestMethod -Uri "http://localhost:5080/api/test/printed-log" -Method Get
$printedJobs | ForEach-Object { Write-Host "  $_" -ForegroundColor Green }

# Cleanup
if ($Cleanup) {
    Write-Host "`n7. Cleaning up..." -ForegroundColor Yellow
    docker-compose -f docker-compose.integration.yml down
}

Write-Host "`n=== Integration Test Complete ===" -ForegroundColor Cyan
```

### 6. Windows Local Testing Guide (USB Printer Simulation)

#### 6.1 Create a "Print to File" Printer on Windows
```powershell
# Create a local port that prints to file
Add-PrinterPort -Name "FILE_ZPL:" -PrinterHostAddress "C:\PrintOutput\zpl_output.txt"
Add-PrinterPort -Name "FILE_PDF:" -PrinterHostAddress "C:\PrintOutput\pdf_output.prn"

# Or use Generic / Text Only driver with FILE: port
Add-Printer -Name "Test-ZPL-Printer" -DriverName "Generic / Text Only" -PortName "FILE:"
Add-Printer -Name "Test-PDF-Printer" -DriverName "Microsoft Print to PDF" -PortName "PORTPROMPT:"

# Alternative: Use built-in "Microsoft XPS Document Writer" or "Microsoft Print to PDF"
```

#### 6.2 Configure Print Service for Local Testing
```json
{
  "PrintService": {
    "Printers": [
      {
        "Name": "LocalTest",
        "Portal": "localhost:5080",
        "Username": "testuser",
        "Password": "testpass",
        "ConnectionName": "test-client",
        "PrinterId": "local-test-001",
        "ZplConnectionType": "Local",
        "ZplPrinterName": "Test-ZPL-Printer",
        "PdfConnectionType": "Local",
        "PdfPrinterName": "Microsoft Print to PDF"
      }
    ]
  }
}
```

#### 6.3 Manual Test Steps
1. Start mock server: `dotnet run --project Techdinamics.Ship.PrintService.MockServer`
2. Add test jobs via API or pre-seed
3. Start print service: `dotnet run --project Techdinamics.Ship.PrintService`
4. Check printer output or file output
5. Verify server log shows jobs marked as printed

### 7. Automated Test Class
Create `IntegrationTests/EndToEndTests.cs`:

```csharp
public class EndToEndTests : IAsyncLifetime
{
    private readonly HttpClient _mockServerClient;
    private IHost _printServiceHost;
    
    [Fact]
    public async Task PrintService_ProcessesZplJob_AndConfirmsPrint()
    {
        // Arrange: Add job to mock server
        await AddTestJob(new PrintJob { /* ZPL only */ });
        
        // Act: Wait for processing
        await Task.Delay(5000);
        
        // Assert: Check file output exists
        Assert.True(File.Exists("/output/zpl_output.txt"));
        
        // Assert: Check job marked as printed
        var printLog = await _mockServerClient.GetFromJsonAsync<List<string>>("/api/test/printed-log");
        Assert.Contains("ORD-001", printLog);
    }
    
    [Fact]
    public async Task PrintService_ProcessesMixedDocuments_ZplAndPdf()
    {
        // Test mixed ZPL + PDF order
    }
    
    [Fact]
    public async Task PrintService_HandlesMultiplePrinters_Concurrently()
    {
        // Test multiple printer configs processing different queues
    }
}
```

## Acceptance Criteria
- Mock server returns XML responses matching legacy API format
- Mock server tracks printed/unprinted state and doesn't return already-printed jobs
- Mock server logs all print confirmations to file
- Docker compose brings up both mock server and print service
- Print service outputs ZPL to text file
- Print service outputs PDF to file
- Integration test script verifies end-to-end flow
- Windows local testing guide allows USB printer simulation
- Supports mixed document types (ZPL labels, PDF labels, PDF packing slips, etc.)
- Test scenarios cover all document type combinations
