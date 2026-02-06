namespace Techdinamics.Ship.PrintService.Models;

/// <summary>
/// Root configuration with array of printers
/// </summary>
public class PrintServiceConfiguration
{
    public List<PrinterConfiguration> Printers { get; set; } = new();
}
