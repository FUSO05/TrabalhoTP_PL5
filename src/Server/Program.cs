using Analysis;
using Grpc.Net.Client;
using One_Health.Server.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Configuration ──────────────────────────────────────────────────────────
builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
    ["Database:Path"]        = "data/onehealth.db",
    ["TcpPort"]              = "8000",
    ["AnalysisService:Url"]  = "http://localhost:50052",
});

// ── Services ───────────────────────────────────────────────────────────────
builder.Services.AddRazorPages();
builder.Services.AddSingleton<DatabaseService>();
builder.Services.AddHostedService<GatewayListenerService>();

// HTTP port for dashboard
builder.WebHost.ConfigureKestrel(k => k.ListenAnyIP(8080));

var app = builder.Build();

// ── Pipeline ───────────────────────────────────────────────────────────────
app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();

// ── REST API ───────────────────────────────────────────────────────────────
var api = app.MapGroup("/api");

// GET /api/measurements
api.MapGet("/measurements", (DatabaseService db,
    string? sensorId, string? zone, string? dataType,
    string? from, string? to, int limit = 100) =>
{
    DateTime? fromDt = from  != null ? DateTime.Parse(from) : null;
    DateTime? toDt   = to    != null ? DateTime.Parse(to)   : null;
    var data = db.QueryMeasurements(sensorId, zone, dataType, fromDt, toDt, limit);
    return Results.Ok(data);
});

// GET /api/analysis
api.MapGet("/analysis", (DatabaseService db) =>
    Results.Ok(db.GetAnalysisResults()));

// POST /api/analysis  — triggers AnalysisService gRPC call
api.MapPost("/analysis", async (DatabaseService db, IConfiguration config, AnalysisRequest req) =>
{
    string analysisUrl = config["AnalysisService:Url"] ?? "http://localhost:50052";

    // Load data points from DB
    DateTime? fromDt = string.IsNullOrWhiteSpace(req.StartTime) ? null : DateTime.Parse(req.StartTime);
    DateTime? toDt   = string.IsNullOrWhiteSpace(req.EndTime)   ? null : DateTime.Parse(req.EndTime);
    var values = db.GetValuesForAnalysis(req.DataType, req.SensorId, fromDt, toDt);

    if (values.Count == 0)
        return Results.BadRequest(new { error = "Sem dados para o período/tipo solicitado." });

    // Call Python gRPC Analysis service
    using var channel = GrpcChannel.ForAddress(analysisUrl);
    var client        = new AnalysisService.AnalysisServiceClient(channel);

    var grpcRequest = new global::Analysis.AnalysisRequest
    {
        SensorId     = req.SensorId ?? "",
        Zone         = req.Zone ?? "",
        DataType     = req.DataType,
        StartTime    = req.StartTime ?? "",
        EndTime      = req.EndTime ?? "",
        AnalysisType = req.AnalysisType ?? "STATISTICS",
    };
    grpcRequest.DataPoints.AddRange(values.Select((v, i) => new DataPoint { Value = v, Timestamp = "" }));

    global::Analysis.AnalysisResult result;
    try { result = await client.AnalyzeAsync(grpcRequest); }
    catch (Exception ex) { return Results.Problem($"AnalysisService indisponível: {ex.Message}"); }

    // Persist result
    var record = new AnalysisRecord(0,
        result.AnalysisType, req.SensorId, req.Zone, req.DataType,
        result.Mean, result.StdDev, result.MinValue, result.MaxValue,
        result.RiskLevel, result.Summary,
        string.Join("|", result.Patterns),
        DateTime.UtcNow.ToString("O"));
    db.StoreAnalysisResult(record);

    return Results.Ok(record with { Id = 0 });
});

// GET /api/sensors
api.MapGet("/sensors", (DatabaseService db) =>
    Results.Ok(db.GetDistinctSensors()));

// GET /api/datatypes
api.MapGet("/datatypes", (DatabaseService db) =>
    Results.Ok(db.GetDistinctDataTypes()));

Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
Console.WriteLine("║   ONE HEALTH SERVER (TP2)                                  ║");
Console.WriteLine("║   TCP :8000 (Gateway)  |  HTTP :8080 (Dashboard)           ║");
Console.WriteLine("╚════════════════════════════════════════════════════════════╝");

app.Run();

// ── DTO ─────────────────────────────────────────────────────────────────────
public record AnalysisRequest(
    string? SensorId, string? Zone, string DataType,
    string? StartTime, string? EndTime, string? AnalysisType);
