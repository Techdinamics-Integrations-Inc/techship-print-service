using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Techdinamics.Ship.PrintService.Models;
using Techdinamics.Ship.PrintService.Services;
using Xunit;
using FluentAssertions;

namespace Techdinamics.Ship.PrintService.Tests;

public class PrinterWorkerTests
{
    private readonly Mock<ITechshipApiClient> _mockApiClient;
    private readonly Mock<IPrintService> _mockPrintService;
    private readonly PrinterConfiguration _config;
    private readonly PrinterWorker _worker;

    public PrinterWorkerTests()
    {
        _mockApiClient = new Mock<ITechshipApiClient>();
        _mockPrintService = new Mock<IPrintService>();
        _config = new PrinterConfiguration
        {
            Name = "Test Worker",
            Enabled = true,
            PollingIntervalMs = 100
        };
        _worker = new PrinterWorker(_mockApiClient.Object, _mockPrintService.Object, NullLogger<PrinterWorker>.Instance);
    }

    [Fact]
    public async Task RunAsync_PrintsAndConfirms_WhenJobReceived()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var job = new PrintJobResponse
        {
            RecordId = "JOB1",
            LabelData = new byte[] { 1, 2, 3 },
            LabelType = "ZPL"
        };

        _mockApiClient.Setup(x => x.GetNextPrintJobAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);
        
        // Stop after one successful loop to avoid infinite loop in test
        _mockPrintService.Setup(x => x.PrintZplAsync(It.IsAny<byte[]>(), It.IsAny<PrinterConfiguration>(), It.IsAny<CancellationToken>()))
            .Callback(() => cts.Cancel())
            .Returns(Task.CompletedTask);

        // Act
        try { await _worker.RunAsync(_config, cts.Token); } catch (OperationCanceledException) { }

        // Assert
        _mockPrintService.Verify(x => x.PrintZplAsync(job.LabelData, _config, It.IsAny<CancellationToken>()), Times.Once);
        _mockApiClient.Verify(x => x.ConfirmPrintAsync("JOB1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_DoesNotConfirm_WhenPrintFails()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var job = new PrintJobResponse
        {
            RecordId = "JOB1",
            LabelData = new byte[] { 1, 2, 3 },
            LabelType = "ZPL"
        };

        _mockApiClient.Setup(x => x.GetNextPrintJobAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);
        
        _mockPrintService.Setup(x => x.PrintZplAsync(It.IsAny<byte[]>(), It.IsAny<PrinterConfiguration>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new System.Exception("Print failed"));

        // Allow some time then cancel
        cts.CancelAfter(500);

        // Act
        try { await _worker.RunAsync(_config, cts.Token); } catch (OperationCanceledException) { }

        // Assert
        _mockApiClient.Verify(x => x.ConfirmPrintAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
