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
    public byte[]? LabelData { get; set; }       // ZPL or PDF content
    public string? LabelType { get; set; }       // "ZPL", "PDF", etc.
    public byte[]? PackingSlipData { get; set; } // Optional packing slip PDF
}
