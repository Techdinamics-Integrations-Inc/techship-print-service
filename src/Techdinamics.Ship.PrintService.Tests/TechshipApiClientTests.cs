using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using Techdinamics.Ship.PrintService.Models;
using Techdinamics.Ship.PrintService.Services;
using Xunit;
using FluentAssertions;

namespace Techdinamics.Ship.PrintService.Tests;

public class TechshipApiClientTests
{
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly HttpClient _httpClient;
    private readonly TechshipApiClient _apiClient;
    private readonly PrinterConfiguration _config;

    public TechshipApiClientTests()
    {
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpMessageHandler.Object);
        _apiClient = new TechshipApiClient(_httpClient, NullLogger<TechshipApiClient>.Instance);
        _config = new PrinterConfiguration
        {
            Portal = "test.techship.io",
            ConnectionName = "test-client",
            ApiSecret = "test-secret"
        };
        _apiClient.Initialize(_config);
    }

    [Fact]
    public async Task GetNextPrintJobAsync_ReturnsJob_WhenJobAvailable()
    {
        // Arrange
        var xmlResponse = @"<Order>
    <OrderId>12345</OrderId>
    <LabelType>ZPL</LabelType>
    <LabelData>W1pQTF0gSGVsbG8gV29ybGQ=</LabelData>
</Order>";
        
        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(xmlResponse)
            });

        // Act
        var result = await _apiClient.GetNextPrintJobAsync();

        // Assert
        result.Should().NotBeNull();
        result!.RecordId.Should().Be("12345");
        result.LabelType.Should().Be("ZPL");
    }

    [Fact]
    public async Task GetNextPrintJobAsync_ReturnsNull_WhenNoJobs()
    {
        // Arrange
        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("<Order />")
            });

        // Act
        var result = await _apiClient.GetNextPrintJobAsync();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetNextPrintJobAsync_ReturnsJob_WhenNewXmlFormatProvided()
    {
        // Arrange
        var xmlResponse = @"<Root>
  <Order>
    <OrderId>184364498</OrderId>
    <BatchNumber>COPY_TEST1739234488-COPYA</BatchNumber>
    <BatchDate>2026-01-28T00:18:28</BatchDate>
    <ClientName>UNITTEST-FEDEXREST</ClientName>
    <CarrierName>FedEx (REST)</CarrierName>
    <PickOrderNumber>TEST1739234488-COPYA-COPY-20260127191828</PickOrderNumber>
    <CustomerOrderNumber>TESTORDER</CustomerOrderNumber>
    <Sequence>0</Sequence>
    <ShipToName>Lucia di Lammermoor</ShipToName>
    <State>MB</State>
    <Country>CA</Country>
  </Order>
  <Labels>
    <Label type=""PDF"" purpose=""LABEL"">TGFiZWxEYXRh</Label>
    <Label type=""PDF"" purpose=""COMMERCIALINVOICE"">UGFja2luZ1NsaXBEYXRh</Label>
  </Labels>
</Root>";
        
        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(xmlResponse)
            });

        // Act
        var result = await _apiClient.GetNextPrintJobAsync();

        // Assert
        result.Should().NotBeNull();
        result!.RecordId.Should().Be("184364498");
        result.LabelData.Should().NotBeNull();
        result.PackingSlipData.Should().NotBeNull();
        result.LabelType.Should().Be("PDF");
    }

    [Fact]
    public async Task GetNextPrintJobAsync_HandlesError_WhenServerReturnsError()
    {
        // Arrange
        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError
            });

        // Act
        var result = await _apiClient.GetNextPrintJobAsync();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ConfirmPrintAsync_SendsCorrectRequest()
    {
        // Arrange
        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == HttpMethod.Post && 
                    req.RequestUri!.ToString().Contains("ConfirmPrint") &&
                    req.RequestUri.ToString().Contains("recordId=12345")),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK
            });

        // Act
        await _apiClient.ConfirmPrintAsync("12345");

        // Assert
        _mockHttpMessageHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>()
        );
    }

    [Fact]
    public async Task GetNextPrintJobAsync_UsesCorrectPortalFromConfig()
    {
        // Arrange
        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.RequestUri!.Host == "test.techship.io"),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("<Order />")
            });

        // Act
        await _apiClient.GetNextPrintJobAsync();

        // Assert
        _mockHttpMessageHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>()
        );
    }

    [Fact]
    public async Task GetNextPrintJobAsync_ReturnsNull_WhenServerReturnsErrorXml()
    {
        // Arrange
        var xmlResponse = "<Error>Invalid printer ID</Error>";
        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(xmlResponse)
            });

        // Act
        var result = await _apiClient.GetNextPrintJobAsync();

        // Assert
        result.Should().BeNull();
    }

}
