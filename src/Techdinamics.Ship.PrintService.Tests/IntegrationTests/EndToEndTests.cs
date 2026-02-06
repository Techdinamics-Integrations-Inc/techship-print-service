using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Techdinamics.Ship.PrintService.Models;
using Techdinamics.Ship.PrintService.Services;
using Xunit;

namespace Techdinamics.Ship.PrintService.Tests.IntegrationTests;

public class EndToEndTests : IAsyncLifetime
{
    private readonly HttpClient _mockServerClient;
    private IHost? _printServiceHost;
    private const string MockServerUrl = "http://localhost:5081";

    public EndToEndTests()
    {
        _mockServerClient = new HttpClient { BaseAddress = new Uri(MockServerUrl) };
    }

    public async Task InitializeAsync()
    {
        // Reset mock server before tests
        try
        {
            await _mockServerClient.PostAsync("/api/test/reset", null);
        }
        catch
        {
            // Mock server might not be running locally, we'll skip or fail depending on environment
        }
    }

    public async Task DisposeAsync()
    {
        if (_printServiceHost != null)
        {
            await _printServiceHost.StopAsync();
            _printServiceHost.Dispose();
        }
        _mockServerClient.Dispose();
    }

    // This test is intended to be run when the Mock Server is already running (e.g., in CI or manually)
    // Or we could try to start it here, but it's simpler to assume it's part of the environment.
    [Fact(Skip = "Requires Mock Server running at http://localhost:5081 and Linux for LP command simulation")]
    public async Task PrintService_ProcessesZplJob_AndConfirmsPrint()
    {
        // Arrange: Add job to mock server
        var job = new
        {
            OrderId = "ORD-INT-001",
            BatchNumber = "BATCH-001",
            ClientName = "Test Client",
            CarrierName = "FedEx",
            Labels = new[]
            {
                new { Type = "DIRECT", Purpose = "LABEL", Data = Convert.ToBase64String("CT_ZPL_DATA"u8.ToArray()) }
            }
        };
        
        var addResponse = await _mockServerClient.PostAsJsonAsync("/api/test/add-job", job);
        addResponse.EnsureSuccessStatusCode();

        // Start Print Service Host pointing to mock server
        _printServiceHost = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                services.Configure<PrintServiceConfiguration>(config =>
                {
                    config.Printers = new List<PrinterConfiguration>
                    {
                        new PrinterConfiguration
                        {
                            Name = "TestPrinter",
                            Portal = MockServerUrl,
                            ConnectionName = "test-client",
                            PrinterId = "test-printer-001",
                            ZplConnectionType = PrinterConnectionType.Network,
                            ZplPrinterAddress = "localhost:9100", // Will likely fail if nothing is listening, but we care about the API call
                            PollingIntervalMs = 1000
                        }
                    };
                });
                services.AddHttpClient<ITechshipApiClient, TechshipApiClient>();
                services.AddSingleton<IPrintService, Services.PrintService>();
                services.AddScoped<IPrinterWorker, PrinterWorker>();
                services.AddSingleton<PrintWorkerManager>();
                services.AddHostedService(sp => sp.GetRequiredService<PrintWorkerManager>());
            })
            .Build();

        await _printServiceHost.StartAsync();

        // Act: Wait for processing
        await Task.Delay(5000);

        // Assert: Check job marked as printed on mock server
        var printLog = await _mockServerClient.GetFromJsonAsync<List<string>>("/api/test/printed-log");
        Assert.NotNull(printLog);
        Assert.Contains(printLog, s => s.Contains("ORD-INT-001"));
    }
}
