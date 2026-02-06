using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Techdinamics.Ship.PrintService.Models;
using Techdinamics.Ship.PrintService.Services;
using Xunit;
using FluentAssertions;

namespace Techdinamics.Ship.PrintService.Tests;

using PrintServiceType = Techdinamics.Ship.PrintService.Services.PrintService;

public class PrintServiceTests
{
    private readonly PrintServiceType _printService;

    public PrintServiceTests()
    {
        _printService = new PrintServiceType(NullLogger<PrintServiceType>.Instance);
    }

    [Fact]
    public async Task PrintZplAsync_SendsCorrectData_WhenUsingNetwork()
    {
        // Arrange
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var config = new PrinterConfiguration
        {
            ZplConnectionType = PrinterConnectionType.Network,
            ZplPrinterAddress = $"127.0.0.1:{port}"
        };
        var expectedData = Encoding.UTF8.GetBytes("^XA^FDHello^FS^XZ");

        try
        {
            // Act
            var printTask = _printService.PrintZplAsync(expectedData, config);

            using var client = await listener.AcceptTcpClientAsync();
            using var stream = client.GetStream();
            var buffer = new byte[expectedData.Length];
            var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);

            await printTask;

            // Assert
            bytesRead.Should().Be(expectedData.Length);
            buffer.Should().Equal(expectedData);
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task PrintZplAsync_ThrowsArgumentException_WhenAddressMissing()
    {
        // Arrange
        var config = new PrinterConfiguration
        {
            ZplConnectionType = PrinterConnectionType.Network,
            ZplPrinterAddress = ""
        };
        var data = Encoding.UTF8.GetBytes("^XA^FDHello^FS^XZ");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _printService.PrintZplAsync(data, config));
    }
}
