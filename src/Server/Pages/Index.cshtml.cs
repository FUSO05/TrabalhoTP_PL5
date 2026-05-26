using Microsoft.AspNetCore.Mvc.RazorPages;
using One_Health.Server.Services;

namespace One_Health.Server.Pages;

public class IndexModel : PageModel
{
    private readonly DatabaseService _db;
    public List<MeasurementRecord> Recent { get; private set; } = [];
    public int TotalMeasurements { get; private set; }
    public int SensorCount       { get; private set; }
    public int ZoneCount         { get; private set; }
    public int AnalysisCount     { get; private set; }

    public IndexModel(DatabaseService db) => _db = db;

    public void OnGet()
    {
        Recent = _db.GetRecentMeasurements(20);
        using var conn = _db.OpenConnection();
        conn.Open();
        TotalMeasurements = Count(conn, "SELECT COUNT(*) FROM measurements");
        SensorCount       = Count(conn, "SELECT COUNT(DISTINCT sensor_id) FROM measurements");
        ZoneCount         = Count(conn, "SELECT COUNT(DISTINCT zone) FROM measurements");
        AnalysisCount     = Count(conn, "SELECT COUNT(*) FROM analysis_results");
    }

    private static int Count(Microsoft.Data.Sqlite.SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return (int)(long)(cmd.ExecuteScalar() ?? 0L);
    }
}
