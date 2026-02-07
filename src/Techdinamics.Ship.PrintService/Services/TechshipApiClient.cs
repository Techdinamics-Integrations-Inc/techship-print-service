using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Techdinamics.Ship.PrintService.Models;

namespace Techdinamics.Ship.PrintService.Services;

public class TechshipApiClient : ITechshipApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TechshipApiClient> _logger;
    private PrinterConfiguration? _config;
    private readonly string _printerId = Guid.NewGuid().ToString();

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
            var endpoint = "Integration/ProcessNextOrderExt";
            var url = $"{endpoint}?clientKey={Uri.EscapeDataString(_config.ConnectionName)}&uid={Uri.EscapeDataString(_printerId)}";
            
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            
            if (!string.IsNullOrEmpty(_config.ApiSecret))
            {
                request.Headers.Add("x-secret-key", _config.ApiSecret);
            }

            var response = await _httpClient.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

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
            var url = $"Integration/ConfirmOrderExt?orderId={Uri.EscapeDataString(recordId)}&clientKey={Uri.EscapeDataString(_config.ConnectionName)}";
            
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            if (!string.IsNullOrEmpty(_config.ApiSecret))
            {
                request.Headers.Add("x-secret-key", _config.ApiSecret);
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

            XElement? orderElement;
            if (root.Name.LocalName == "Root")
            {
                orderElement = root.Element("Order") ?? root.Element("Pallet");
            }
            else
            {
                orderElement = root;
            }

            if (orderElement == null) return null;

            var result = new PrintJobResponse
            {
                RecordId = orderElement.Element("OrderId")?.Value ?? orderElement.Element("PalletId")?.Value,
                BatchNumber = orderElement.Element("BatchNumber")?.Value,
                ClientName = orderElement.Element("ClientName")?.Value,
                CarrierName = orderElement.Element("CarrierName")?.Value,
                PickOrderNumber = orderElement.Element("PickOrderNumber")?.Value,
                CustomerOrderNumber = orderElement.Element("CustomerOrderNumber")?.Value,
                Sequence = orderElement.Element("Sequence")?.Value,
                ShipToName = orderElement.Element("ShipToName")?.Value,
                State = orderElement.Element("State")?.Value,
                Country = orderElement.Element("Country")?.Value,
            };

            if (DateTime.TryParse(orderElement.Element("BatchDate")?.Value, out var batchDate))
            {
                result.BatchDate = batchDate;
            }

            // Parse Labels
            var labelsElement = root.Element("Labels");
            if (labelsElement != null)
            {
                foreach (var labelElement in labelsElement.Elements("Label"))
                {
                    var label = new PrintJobLabel
                    {
                        Type = labelElement.Attribute("type")?.Value,
                        Purpose = labelElement.Attribute("purpose")?.Value,
                        Data = !string.IsNullOrEmpty(labelElement.Value) ? Convert.FromBase64String(labelElement.Value) : null
                    };
                    result.Labels.Add(label);
                }
            }
            else
            {
                // Fallback to legacy single label format if <Labels> is missing
                var labelDataBase64 = orderElement.Element("LabelData")?.Value;
                if (!string.IsNullOrEmpty(labelDataBase64))
                {
                    result.Labels.Add(new PrintJobLabel
                    {
                        Type = orderElement.Element("LabelType")?.Value ?? "ZPL",
                        Purpose = "LABEL",
                        Data = Convert.FromBase64String(labelDataBase64)
                    });
                }

                var packingSlipDataBase64 = orderElement.Element("PackingSlipData")?.Value;
                if (!string.IsNullOrEmpty(packingSlipDataBase64))
                {
                    result.Labels.Add(new PrintJobLabel
                    {
                        Type = "PDF",
                        Purpose = "COMMERCIALINVOICE",
                        Data = Convert.FromBase64String(packingSlipDataBase64)
                    });
                }
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
