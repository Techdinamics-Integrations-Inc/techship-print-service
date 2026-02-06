using System.Net.Http.Headers;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Techdinamics.Ship.PrintService.Models;

namespace Techdinamics.Ship.PrintService.Services;

public class TechshipApiClient : ITechshipApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TechshipApiClient> _logger;
    private PrinterConfiguration? _config;
    private string? _apiKey;

    public TechshipApiClient(HttpClient httpClient, ILogger<TechshipApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public void Initialize(PrinterConfiguration config)
    {
        _config = config;
        var baseUrl = _config.Portal.StartsWith("http") ? _config.Portal : $"https://{_config.Portal}";
        if (!baseUrl.EndsWith("/")) baseUrl += "/";
        _httpClient.BaseAddress = new Uri(baseUrl);
    }

    public async Task<PrintJobResponse?> GetNextPrintJobAsync(CancellationToken ct = default)
    {
        if (_config == null) throw new InvalidOperationException("Client not initialized");

        try
        {
            var endpoint = _config.UsePalletEndpoint
                ? "Print/ProcessNextPalletExt"
                : "Integration/ProcessNextOrderExt";
            var url = $"{endpoint}?clientKey={Uri.EscapeDataString(_config.ConnectionName)}&uid={Uri.EscapeDataString(_config.PrinterId)}";
            
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            
            if (!string.IsNullOrEmpty(_config.ApiSecret))
            {
                request.Headers.Add("x-secret-key", _config.ApiSecret);
            }

            if (!string.IsNullOrEmpty(_apiKey))
            {
                request.Headers.Add("x-desktop-api-key", _apiKey);
            }

            var response = await _httpClient.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            // Extract API key if present
            if (response.Headers.TryGetValues("set-desktop-api-key", out var values))
            {
                _apiKey = values.FirstOrDefault();
            }

            var xmlContent = await response.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(xmlContent)) return null;

            return ParsePrintJobResponse(xmlContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching next print job from {Portal}", _config.Portal);
            return null;
        }
    }

    public async Task ConfirmPrintAsync(string recordId, CancellationToken ct = default)
    {
        if (_config == null) throw new InvalidOperationException("Client not initialized");

        try
        {
            // Note: Exact confirmation endpoint might need verification against legacy app
            // For now, following the pattern in the plan
            var url = $"Integration/ConfirmPrint?recordId={Uri.EscapeDataString(recordId)}&clientKey={Uri.EscapeDataString(_config.ConnectionName)}";
            
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            if (!string.IsNullOrEmpty(_config.ApiSecret))
            {
                request.Headers.Add("x-secret-key", _config.ApiSecret);
            }
            if (!string.IsNullOrEmpty(_apiKey))
            {
                request.Headers.Add("x-desktop-api-key", _apiKey);
            }

            var response = await _httpClient.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error confirming print for {RecordId} on {Portal}", recordId, _config.Portal);
        }
    }

    private PrintJobResponse? ParsePrintJobResponse(string xml)
    {
        try
        {
            var doc = XDocument.Parse(xml);
            var root = doc.Root;
            if (root == null || root.Name.LocalName == "Error")
            {
                var error = root?.Value;
                if (!string.IsNullOrEmpty(error)) _logger.LogWarning("API returned error: {Error}", error);
                return null;
            }

            // The legacy XML could be <Order> or <Pallet>
            var result = new PrintJobResponse
            {
                RecordId = root.Element("OrderId")?.Value ?? root.Element("PalletId")?.Value,
                BatchNumber = root.Element("BatchNumber")?.Value,
                ClientName = root.Element("ClientName")?.Value,
                CarrierName = root.Element("CarrierName")?.Value,
                PickOrderNumber = root.Element("PickOrderNumber")?.Value,
                CustomerOrderNumber = root.Element("CustomerOrderNumber")?.Value,
                Sequence = root.Element("Sequence")?.Value,
                ShipToName = root.Element("ShipToName")?.Value,
                State = root.Element("State")?.Value,
                Country = root.Element("Country")?.Value,
                LabelType = root.Element("LabelType")?.Value ?? "ZPL"
            };

            if (DateTime.TryParse(root.Element("BatchDate")?.Value, out var batchDate))
            {
                result.BatchDate = batchDate;
            }

            var labelDataBase64 = root.Element("LabelData")?.Value;
            if (!string.IsNullOrEmpty(labelDataBase64))
            {
                result.LabelData = Convert.FromBase64String(labelDataBase64);
            }

            var packingSlipDataBase64 = root.Element("PackingSlipData")?.Value;
            if (!string.IsNullOrEmpty(packingSlipDataBase64))
            {
                result.PackingSlipData = Convert.FromBase64String(packingSlipDataBase64);
            }

            return result.RecordId != null ? result : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing XML response");
            return null;
        }
    }
}
