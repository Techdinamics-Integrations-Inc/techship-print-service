namespace Techdinamics.Ship.PrintService.Models;

public class WorkerHealthStatus
{
    public string PrinterName { get; set; } = string.Empty;
    public bool IsHealthy { get; set; }
    public DateTime? LastSuccessfulPoll { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
}
