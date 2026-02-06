# Subtask 06: Unit Test Project with Mocked API

## Objective
Create a unit test project to test all components with mocked server API, including multi-printer concurrent worker scenarios.

## Requirements

### 1. Create Test Project
Create `Techdinamics.Ship.PrintService.Tests` project:
```bash
dotnet new xunit -n Techdinamics.Ship.PrintService.Tests
dotnet add reference ../Techdinamics.Ship.PrintService/Techdinamics.Ship.PrintService.csproj
```

Add packages:
- `Moq` - for mocking interfaces
- `FluentAssertions` - for readable assertions
- `Microsoft.Extensions.Logging.Abstractions` - for NullLogger

### 2. Create Mock API Server (Optional)
For integration-style tests, create `MockTechshipServer.cs`:
- Use `WireMock.Net` or simple in-memory HTTP server
- Return predefined XML responses
- Simulate various scenarios (success, no jobs, errors)

### 3. Test TechshipApiClient
Create `TechshipApiClientTests.cs`:

```csharp
public class TechshipApiClientTests
{
    [Fact]
    public async Task GetNextPrintJobAsync_ReturnsJob_WhenJobAvailable()
    
    [Fact]
    public async Task GetNextPrintJobAsync_ReturnsNull_WhenNoJobs()
    
    [Fact]
    public async Task GetNextPrintJobAsync_HandlesError_WhenServerReturnsError()
    
    [Fact]
    public async Task ConfirmPrintAsync_SendsCorrectRequest()
    
    [Fact]
    public async Task GetNextPrintJobAsync_UsesApiSecret_WhenConfigured()
    
    [Fact]
    public async Task GetNextPrintJobAsync_UsesCorrectPortalFromConfig()
}
```

### 4. Test PrintService
Create `PrintServiceTests.cs`:

```csharp
public class PrintServiceTests
{
    [Fact]
    public async Task PrintZplAsync_SendsDataToCorrectAddress()
    
    [Fact]
    public async Task PrintZplAsync_ThrowsOnConnectionFailure()
    
    [Fact]
    public async Task PrintPdfAsync_ProcessesPdfCorrectly()
    
    [Fact]
    public async Task PrintZplAsync_UsesConfiguredPrinterAddress()
}
```

### 5. Test PrinterWorker
Create `PrinterWorkerTests.cs`:

```csharp
public class PrinterWorkerTests
{
    private readonly Mock<ITechshipApiClient> _mockApiClient;
    private readonly Mock<IPrintService> _mockPrintService;
    
    [Fact]
    public async Task RunAsync_PollsApiAtConfiguredInterval()
    
    [Fact]
    public async Task RunAsync_PrintsAndConfirms_WhenJobReceived()
    
    [Fact]
    public async Task RunAsync_DoesNotConfirm_WhenPrintFails()
    
    [Fact]
    public async Task RunAsync_ContinuesOnError()
    
    [Fact]
    public async Task RunAsync_StopsGracefully_OnCancellation()
    
    [Fact]
    public async Task RunAsync_UsesCorrectPrinterConfig()
}
```

### 6. Test PrintWorkerManager (Multi-Printer)
Create `PrintWorkerManagerTests.cs`:

```csharp
public class PrintWorkerManagerTests
{
    [Fact]
    public async Task ExecuteAsync_StartsWorkerForEachEnabledPrinter()
    
    [Fact]
    public async Task ExecuteAsync_SkipsDisabledPrinters()
    
    [Fact]
    public async Task ExecuteAsync_WorkersRunConcurrently()
    
    [Fact]
    public async Task ExecuteAsync_OneWorkerFailureDoesNotAffectOthers()
    
    [Fact]
    public async Task ExecuteAsync_AllWorkersStopOnCancellation()
    
    [Fact]
    public async Task ExecuteAsync_HandlesEmptyPrinterList()
}
```

### 7. Test Configuration
Create `ConfigurationTests.cs`:

```csharp
public class ConfigurationTests
{
    [Fact]
    public void Configuration_LoadsFromEnvironmentVariables()
    
    [Fact]
    public void Configuration_LoadsMultiplePrintersFromJson()
    
    [Fact]
    public void Configuration_LoadsMultiplePrintersFromIndexedEnvVars()
    
    [Fact]
    public void Configuration_HasCorrectDefaults()
    
    [Fact]
    public void PrinterConfiguration_EnabledDefaultsToTrue()
}
```

### 8. Sample XML Responses
Create `TestData/` folder with sample XML responses:
- `order_response.xml` - successful order with label
- `empty_response.xml` - no pending orders
- `error_response.xml` - error from server

### 9. Add to Solution
```bash
dotnet sln add Techdinamics.Ship.PrintService.Tests/Techdinamics.Ship.PrintService.Tests.csproj
```

## Acceptance Criteria
- All unit tests pass
- API client is fully tested with mocked HTTP responses
- Print service is tested with mocked network connections
- PrinterWorker is tested with mocked dependencies
- PrintWorkerManager is tested for multi-printer concurrent scenarios
- Test coverage for error scenarios including worker isolation
- Tests verify workers run independently and don't affect each other
- Tests run in CI/CD pipeline
