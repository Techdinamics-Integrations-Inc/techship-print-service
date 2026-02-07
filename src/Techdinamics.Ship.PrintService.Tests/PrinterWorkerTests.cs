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
            RecordId = "JOB1"
        };
        job.Labels.Add(new PrintJobLabel { Purpose = "LABEL", Type = "ZPL", Data = new byte[] { 1, 2, 3 } });

        _mockApiClient.Setup(x => x.GetNextPrintJobAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);
        
        // Use a signal to cancel after confirmation is expected to have happened
        _mockApiClient.Setup(x => x.ConfirmPrintAsync("JOB1", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockPrintService.Setup(x => x.PrintZplAsync(It.IsAny<byte[]>(), It.IsAny<PrinterConfiguration>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback(() => cts.Cancel());

        // Act
        try { await _worker.RunAsync(_config, cts.Token); } catch (OperationCanceledException) { }

        // Assert
        _mockPrintService.Verify(x => x.PrintZplAsync(It.IsAny<byte[]>(), _config, It.IsAny<CancellationToken>()), Times.Once);
        _mockApiClient.Verify(x => x.ConfirmPrintAsync("JOB1", It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task RunAsync_PrintsMultipleLabels_WhenJobHasMany()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var job = new PrintJobResponse
        {
            RecordId = "MULTI_JOB"
        };
        job.Labels.Add(new PrintJobLabel { Purpose = "LABEL", Type = "ZPL", Data = new byte[] { 1 } });
        job.Labels.Add(new PrintJobLabel { Purpose = "LABEL", Type = "ZPL", Data = new byte[] { 2 } });
        job.Labels.Add(new PrintJobLabel { Purpose = "PACKINGSLIP", Type = "PDF", Data = new byte[] { 3 } });

        _mockApiClient.Setup(x => x.GetNextPrintJobAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);
        
        int printCount = 0;
        _mockPrintService.Setup(x => x.PrintZplAsync(It.IsAny<byte[]>(), It.IsAny<PrinterConfiguration>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback(() => { if (++printCount >= 3) cts.Cancel(); });
            
        _mockPrintService.Setup(x => x.PrintPdfAsync(It.IsAny<byte[]>(), It.IsAny<PrinterConfiguration>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback(() => { if (++printCount >= 3) cts.Cancel(); });

        // Act
        try { await _worker.RunAsync(_config, cts.Token); } catch (OperationCanceledException) { }

        // Assert
        _mockPrintService.Verify(x => x.PrintZplAsync(It.IsAny<byte[]>(), _config, It.IsAny<CancellationToken>()), Times.Exactly(2));
        _mockPrintService.Verify(x => x.PrintPdfAsync(It.IsAny<byte[]>(), _config, false, It.IsAny<CancellationToken>()), Times.Once);
        _mockApiClient.Verify(x => x.ConfirmPrintAsync("MULTI_JOB", It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task RunAsync_ConfirmsImmediately_EvenIfPrintFailsLater()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var job = new PrintJobResponse
        {
            RecordId = "JOB1"
        };
        job.Labels.Add(new PrintJobLabel { Purpose = "LABEL", Type = "ZPL", Data = new byte[] { 1, 2, 3 } });

        _mockApiClient.Setup(x => x.GetNextPrintJobAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);
        
        _mockPrintService.Setup(x => x.PrintZplAsync(It.IsAny<byte[]>(), It.IsAny<PrinterConfiguration>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new System.Exception("Print failed"));

        // Use a signal to cancel after confirmation is expected to have happened
        _mockApiClient.Setup(x => x.ConfirmPrintAsync("JOB1", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback(() => cts.Cancel());

        // Act
        try { await _worker.RunAsync(_config, cts.Token); } catch (OperationCanceledException) { }

        // Assert
        // Confirm should be called because it happens before the queue processing (which fails)
        _mockApiClient.Verify(x => x.ConfirmPrintAsync("JOB1", It.IsAny<CancellationToken>()), Times.Once);
    }
}
