namespace Techdinamics.Ship.PrintService.Models;

public class PrintJobResponse
{
    public string? RecordId { get; set; }        // OrderId or PalletId
    public string? BatchNumber { get; set; }
    public DateTime? BatchDate { get; set; }
    public string? ClientName { get; set; }
    public string? CarrierName { get; set; }
    public string? PickOrderNumber { get; set; }
    public string? CustomerOrderNumber { get; set; }
    public string? Sequence { get; set; }
    public string? ShipToName { get; set; }
    public string? State { get; set; }
    public string? Country { get; set; }
    
    // Label data
    public List<PrintJobLabel> Labels { get; set; } = new();

    // Compatibility properties (legacy support or simplified access)
    public byte[]? LabelData => Labels.FirstOrDefault(l => l.Purpose == "LABEL")?.Data;
    public string? LabelType => Labels.FirstOrDefault(l => l.Purpose == "LABEL")?.Type;
    public byte[]? PackingSlipData => Labels.FirstOrDefault(l => l.Purpose == "COMMERCIALINVOICE" || l.Purpose == "PACKINGSLIP")?.Data;
}

public class PrintJobLabel
{
    public string? Type { get; set; }    // "PDF", "ZPL", etc.
    public string? Purpose { get; set; } // "LABEL", "COMMERCIALINVOICE", etc.
    public byte[]? Data { get; set; }
}
