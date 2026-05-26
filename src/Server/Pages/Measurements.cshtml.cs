using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;
using One_Health.Server.Services;

namespace One_Health.Server.Pages;

public class MeasurementsModel : PageModel
{
    private readonly DatabaseService _db;
    public const int PageSize = 20;

    public List<MeasurementRecord> Results { get; private set; } = [];
    public string SensorConfigJson { get; private set; } = "[]";
    public int TotalCount  { get; private set; }
    public int TotalPages  { get; private set; }
    public int CurrentPage { get; private set; }

    [BindProperty(Name = "sensorId",  SupportsGet = true)] public string? FilterSensorId { get; set; }
    [BindProperty(Name = "zone",      SupportsGet = true)] public string? FilterZone     { get; set; }
    [BindProperty(Name = "dataType",  SupportsGet = true)] public string? FilterDataType { get; set; }
    [BindProperty(Name = "from",      SupportsGet = true)] public string? FilterFrom     { get; set; }
    [BindProperty(Name = "to",        SupportsGet = true)] public string? FilterTo       { get; set; }

    public MeasurementsModel(DatabaseService db) => _db = db;

    public void OnGet()
    {
        var sensors = SensorConfigReader.Load().Where(s => s.Status == "ativo").ToList();
        SensorConfigJson = JsonSerializer.Serialize(sensors,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        DateTime? from = string.IsNullOrWhiteSpace(FilterFrom) ? null : DateTime.Parse(FilterFrom);
        DateTime? to   = string.IsNullOrWhiteSpace(FilterTo)   ? null : DateTime.Parse(FilterTo);

        int requestedPage = int.TryParse(Request.Query["page"], out var p) && p > 0 ? p : 1;

        TotalCount  = _db.CountMeasurements(FilterSensorId, FilterZone, FilterDataType, from, to);
        TotalPages  = Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
        CurrentPage = Math.Clamp(requestedPage, 1, TotalPages);

        int offset = (CurrentPage - 1) * PageSize;
        Results = _db.QueryMeasurements(FilterSensorId, FilterZone, FilterDataType, from, to, PageSize, offset);
    }

    // Preserve all current query params, replace only page=p
    public string PageUrl(int p)
    {
        var parts = HttpContext.Request.Query
            .Where(q => q.Key != "page")
            .Select(q => $"{q.Key}={Uri.EscapeDataString(q.Value.ToString())}")
            .Append($"page={p}");
        return "?" + string.Join("&", parts);
    }
}
