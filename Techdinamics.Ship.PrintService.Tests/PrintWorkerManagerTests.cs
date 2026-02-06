using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Techdinamics.Ship.PrintService.Models;
using Techdinamics.Ship.PrintService.Services;
using Xunit;
using FluentAssertions;

namespace Techdinamics.Ship.PrintService.Tests;

public class PrintWorkerManagerTests
{
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<IServiceScopeFactory> _mockScopeFactory;
    private readonly Mock<IServiceScope> _mockScope;
    private readonly Mock<IPrinterWorker> _mockWorker;
    private readonly List<PrinterConfiguration> _printers;
    private readonly PrintWorkerManager _manager;

    public PrintWorkerManagerTests()
    {
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockScopeFactory = new Mock<IServiceScopeFactory>();
        _mockScope = new Mock<IServiceScope>();
        _mockWorker = new Mock<IPrinterWorker>();

        _mockServiceProvider.Setup(x => x.GetService(typeof(IServiceScopeFactory))).Returns(_mockScopeFactory.Object);
        _mockScopeFactory.Setup(x => x.CreateScope()).Returns(_mockScope.Object);
        _mockScope.Setup(x => x.ServiceProvider).Returns(_mockServiceProvider.Object);
        _mockServiceProvider.Setup(x => x.GetService(typeof(IPrinterWorker))).Returns(_mockWorker.Object);

        _printers = new List<PrinterConfiguration>();
        var options = Options.Create(new PrintServiceConfiguration { Printers = _printers });

        _manager = new PrintWorkerManager(options, _mockServiceProvider.Object, NullLogger<PrintWorkerManager>.Instance);
    }

    [Fact]
    public async Task ExecuteAsync_StartsWorkerForEachEnabledPrinter()
    {
        // Arrange
        _printers.Add(new PrinterConfiguration { Name = "P1", Enabled = true });
        _printers.Add(new PrinterConfiguration { Name = "P2", Enabled = true });
        _printers.Add(new PrinterConfiguration { Name = "P3", Enabled = false });

        var cts = new CancellationTokenSource();
        
        // Setup workers to wait for cancellation to simulate running
        _mockWorker.Setup(x => x.RunAsync(It.IsAny<PrinterConfiguration>(), It.IsAny<CancellationToken>()))
            .Returns(async (PrinterConfiguration cfg, CancellationToken ct) => 
            {
                await Task.Delay(-1, ct);
            });

        // Act
        var task = _manager.StartAsync(cts.Token);
        await Task.Delay(200); // Give it a moment to start workers
        await _manager.StopAsync(cts.Token);

        // Assert
        _mockWorker.Verify(x => x.RunAsync(It.Is<PrinterConfiguration>(p => p.Name == "P1"), It.IsAny<CancellationToken>()), Times.Once);
        _mockWorker.Verify(x => x.RunAsync(It.Is<PrinterConfiguration>(p => p.Name == "P2"), It.IsAny<CancellationToken>()), Times.Once);
        _mockWorker.Verify(x => x.RunAsync(It.Is<PrinterConfiguration>(p => p.Name == "P3"), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_OneWorkerFailureDoesNotAffectOthers()
    {
        // Arrange
        _printers.Add(new PrinterConfiguration { Name = "Faulty", Enabled = true });
        _printers.Add(new PrinterConfiguration { Name = "Stable", Enabled = true });

        var cts = new CancellationTokenSource();

        _mockWorker.Setup(x => x.RunAsync(It.Is<PrinterConfiguration>(p => p.Name == "Faulty"), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("KABOOM"));

        _mockWorker.Setup(x => x.RunAsync(It.Is<PrinterConfiguration>(p => p.Name == "Stable"), It.IsAny<CancellationToken>()))
            .Returns(async (PrinterConfiguration cfg, CancellationToken ct) => 
            {
                await Task.Delay(-1, ct);
            });

        // Act
        await _manager.StartAsync(cts.Token);
        await Task.Delay(200);
        await _manager.StopAsync(cts.Token);

        // Assert
        _mockWorker.Verify(x => x.RunAsync(It.Is<PrinterConfiguration>(p => p.Name == "Stable"), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }
}
