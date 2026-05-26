using Microsoft.AspNetCore.Mvc.RazorPages;
using One_Health.Server.Services;

namespace One_Health.Server.Pages;

public record SensorStat(string SensorId, List<string> DataTypes, int Count);
public record CsvSensorInfo(string SensorId, string Status, string Zone, List<string> DataTypes);

public class SensorsPageModel : PageModel
{
    private readonly DatabaseService _db;

    public List<CsvSensorInfo> AllSensors { get; private set; } = [];
    public Dictionary<string, SensorStat> DbStats { get; private set; } = [];

    // Keep for backwards compat with existing template references
    public List<string> SensorIds { get; private set; } = [];
    public List<SensorStat> SensorStats { get; private set; } = [];

    public SensorsPageModel(DatabaseService db) => _db = db;

    public void OnGet()
    {
        LoadCsvSensors();
        LoadDbStats();

        SensorIds   = AllSensors.Select(s => s.SensorId).ToList();
        SensorStats = AllSensors
            .Select(s => DbStats.TryGetValue(s.SensorId, out var stat)
                ? stat
                : new SensorStat(s.SensorId, s.DataTypes, 0))
            .ToList();
    }

    private void LoadCsvSensors()
    {
        // Server CWD is src/Server; config/ lives two levels up at the solution root
        string csvPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "config", "sensors.csv"));
        if (!System.IO.File.Exists(csvPath))
        {
            // Fallback: try from CWD directly (if run from solution root)
            csvPath = Path.Combine(Directory.GetCurrentDirectory(), "config", "sensors.csv");
        }
        if (!System.IO.File.Exists(csvPath)) return;

        foreach (var line in System.IO.File.ReadLines(csvPath))
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#") ||
                line.StartsWith("sensor_id", StringComparison.OrdinalIgnoreCase))
                continue;

            var parts = line.Split(':', 5);
            if (parts.Length < 4) continue;

            string id     = parts[0].Trim();
            string status = parts[1].Trim();
            string zone   = parts[2].Trim();

            string typesPart = parts[3].Trim().TrimStart('[').TrimEnd(']');
            var types = typesPart
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToList();

            AllSensors.Add(new CsvSensorInfo(id, status, zone, types));
        }
    }

    private void LoadDbStats()
    {
        var sensorIds = _db.GetDistinctSensors();
        using var conn = _db.OpenConnection();
        conn.Open();

        foreach (var sid in sensorIds)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT DISTINCT data_type FROM measurements WHERE sensor_id = $sid";
            cmd.Parameters.AddWithValue("$sid", sid);
            var types = new List<string>();
            using (var r = cmd.ExecuteReader())
                while (r.Read()) types.Add(r.GetString(0));

            using var cmd2 = conn.CreateCommand();
            cmd2.CommandText = "SELECT COUNT(*) FROM measurements WHERE sensor_id = $sid";
            cmd2.Parameters.AddWithValue("$sid", sid);
            int count = (int)(long)(cmd2.ExecuteScalar() ?? 0L);

            DbStats[sid] = new SensorStat(sid, types, count);
        }
    }
}
