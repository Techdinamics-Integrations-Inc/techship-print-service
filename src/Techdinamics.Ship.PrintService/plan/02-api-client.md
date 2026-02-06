# Subtask 02: Web API Client Implementation

## Objective
Implement the API client to communicate with the Techship web portal for fetching print jobs and confirming prints.

## Requirements

### 1. Create API Response Models
Create `Models/PrintJobResponse.cs` based on legacy XML response:

```csharp
public class PrintJobResponse
{
    public string RecordId { get; set; }        // OrderId or PalletId
    public string BatchNumber { get; set; }
    public DateTime? BatchDate { get; set; }
    public string ClientName { get; set; }
    public string CarrierName { get; set; }
    public string PickOrderNumber { get; set; }
    public string CustomerOrderNumber { get; set; }
    public string Sequence { get; set; }
    public string ShipToName { get; set; }
    public string State { get; set; }
    public string Country { get; set; }
    
    // Label data
    public byte[] LabelData { get; set; }       // ZPL or PDF content
    public string LabelType { get; set; }       // "ZPL", "PDF", etc.
    public byte[] PackingSlipData { get; set; } // Optional packing slip PDF
}
```

### 2. Create ITechshipApiClient Interface
Create `Services/ITechshipApiClient.cs`:

```csharp
public interface ITechshipApiClient
{
    Task<PrintJobResponse?> GetNextPrintJobAsync(CancellationToken ct = default);
    Task ConfirmPrintAsync(string recordId, CancellationToken ct = default);
}
```

### 3. Implement TechshipApiClient
Create `Services/TechshipApiClient.cs`:
- Use HttpClient via IHttpClientFactory
- Implement authentication (API secret header)
- Parse XML response from legacy endpoint `Integration/ProcessNextOrderExt`
- Handle CrossDock variant endpoint `Print/ProcessNextPalletExt`
- Implement confirmation endpoint call
- Handle API key extraction from response headers (`set-desktop-api-key`)
- Proper error handling and logging

### 4. Reference Legacy Implementation
Key details from `ProcessingHostViewModel.cs`:
- Base URL: `https://{portal}`
- Endpoint: `Integration/ProcessNextOrderExt?clientKey={connectionName}&uid={printerId}`
- If ApiSecret exists, add header: `x-secret-key: {apiSecret}`
- Response is XML with Order/Pallet element containing label data

### 5. Register in DI
Register `ITechshipApiClient` and configure HttpClient in Program.cs.

## Acceptance Criteria
- API client can authenticate with portal
- Can fetch next print job and parse response
- Can confirm print completion
- Handles errors gracefully with logging
- Interface allows for easy mocking in tests
