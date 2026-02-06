using System.Collections.Concurrent;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<PrintJobStore>();

var app = builder.Build();

app.MapGet("/", () => "Techship Print Service Mock Server");

// --- Legacy API Endpoints ---

app.MapPost("/Integration/ProcessNextOrderExt", (HttpContext ctx, PrintJobStore store) =>
{
    var clientKey = ctx.Request.Query["clientKey"];
    var printerId = ctx.Request.Query["uid"];
    
    // In legacy, authentication was via form data or x-secret-key header
    // For mock, we just log it if needed
    
    var job = store.GetNextUnprinted(clientKey.ToString(), printerId.ToString());
    if (job == null)
        return Results.Content("<Response></Response>", "application/xml");
    
    return Results.Content(BuildOrderXml(job), "application/xml");
});

app.MapPost("/Integration/ConfirmPrint", (HttpContext ctx, PrintJobStore store) =>
{
    var orderId = ctx.Request.Query["orderId"].ToString();
    if (string.IsNullOrEmpty(orderId))
    {
        // Support legacy/alternative param name used by client
        orderId = ctx.Request.Query["recordId"].ToString();
    }
    if (!string.IsNullOrEmpty(orderId))
    {
        store.MarkAsPrinted(orderId);
        store.LogPrintAction(orderId, "CONFIRMED");
        return Results.Ok();
    }
    return Results.BadRequest("Missing orderId/recordId");
});

// --- Test Setup Endpoints ---

app.MapPost("/api/test/add-job", (PrintJob job, PrintJobStore store) =>
{
    store.AddJob(job);
    return Results.Ok(new { job.OrderId });
});

app.MapGet("/api/test/jobs", (PrintJobStore store) => store.GetAllJobs());
app.MapGet("/api/test/printed-log", (PrintJobStore store) => store.GetPrintLog());
app.MapPost("/api/test/reset", (PrintJobStore store) => { store.Reset(); return Results.Ok(); });

app.Run();

string BuildOrderXml(PrintJob job)
{
    var sb = new StringBuilder();
    sb.AppendLine("<Response>");
    sb.AppendLine($"  <OrderId>{job.OrderId}</OrderId>");
    sb.AppendLine($"  <BatchNumber>{job.BatchNumber}</BatchNumber>");
    sb.AppendLine($"  <ClientName>{job.ClientName}</ClientName>");
    sb.AppendLine($"  <CarrierName>{job.CarrierName}</CarrierName>");

    var mainLabel = job.Labels.FirstOrDefault(l => l.Purpose == "LABEL");
    if (mainLabel != null)
    {
        sb.AppendLine($"  <LabelType>{mainLabel.Type}</LabelType>");
        sb.AppendLine($"  <LabelData>{Convert.ToBase64String(mainLabel.Data)}</LabelData>");
    }

    var packingSlip = job.Labels.FirstOrDefault(l => l.Purpose == "PACKINGSLIP");
    if (packingSlip != null)
    {
        sb.AppendLine($"  <PackingSlipData>{Convert.ToBase64String(packingSlip.Data)}</PackingSlipData>");
    }

    // Additional labels could be added if TechshipApiClient is updated to support them
    
    sb.AppendLine("</Response>");
    return sb.ToString();
}

public class PrintJob
{
    public string OrderId { get; set; } = string.Empty;
    public string BatchNumber { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string CarrierName { get; set; } = string.Empty;
    public List<PrintLabel> Labels { get; set; } = new();
    public bool IsPrinted { get; set; }
    public DateTime? PrintedAt { get; set; }
}

public class PrintLabel
{
    public string Type { get; set; } = string.Empty;      // "DIRECT" (ZPL), "PDF", "IMAGE"
    public string Purpose { get; set; } = string.Empty;   // "LABEL", "PACKINGSLIP", "COMMERCIALINVOICE", "DGDECLARATION"
    public byte[] Data { get; set; } = Array.Empty<byte>();
}

public class PrintJobStore
{
    private readonly ConcurrentDictionary<string, PrintJob> _jobs = new();
    private readonly ConcurrentQueue<string> _printLog = new();
    
    public void AddJob(PrintJob job)
    {
        _jobs[job.OrderId] = job;
    }

    public PrintJob? GetNextUnprinted(string clientKey, string printerId)
    {
        return _jobs.Values.FirstOrDefault(j => !j.IsPrinted);
    }

    public void MarkAsPrinted(string orderId)
    {
        if (_jobs.TryGetValue(orderId, out var job))
        {
            job.IsPrinted = true;
            job.PrintedAt = DateTime.UtcNow;
        }
    }

    public void LogPrintAction(string orderId, string action)
    {
        _printLog.Enqueue($"{DateTime.UtcNow:O} - Order {orderId}: {action}");
    }

    public IEnumerable<PrintJob> GetAllJobs() => _jobs.Values;

    public IEnumerable<string> GetPrintLog() => _printLog;

    public void Reset()
    {
        _jobs.Clear();
        _printLog.Clear();
    }
}
