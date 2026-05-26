using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;
using One_Health.Server.Services;

namespace One_Health.Server.Pages;

public class AnalysisPageModel : PageModel
{
    private readonly DatabaseService _db;
    public List<AnalysisRecord> Results { get; private set; } = [];
    public string SensorConfigJson { get; private set; } = "[]";

    public AnalysisPageModel(DatabaseService db) => _db = db;

    public void OnGet()
    {
        Results = _db.GetAnalysisResults(50);
        var sensors = SensorConfigReader.Load().Where(s => s.Status == "ativo").ToList();
        SensorConfigJson = JsonSerializer.Serialize(sensors,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }
}
