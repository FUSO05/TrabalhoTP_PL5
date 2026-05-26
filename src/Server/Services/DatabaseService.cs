using Microsoft.Data.Sqlite;

namespace One_Health.Server.Services;

public class DatabaseService
{
    private readonly string _connectionString;

    public DatabaseService(IConfiguration configuration)
    {
        string dbPath = configuration["Database:Path"] ?? "data/onehealth.db";
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath) ?? "data");
        _connectionString = $"Data Source={dbPath}";
        InitializeSchema();
    }

    private void InitializeSchema()
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS measurements (
                id        INTEGER PRIMARY KEY AUTOINCREMENT,
                sensor_id TEXT NOT NULL,
                zone      TEXT NOT NULL,
                data_type TEXT NOT NULL,
                value     REAL NOT NULL,
                timestamp TEXT NOT NULL,
                stored_at TEXT NOT NULL DEFAULT (datetime('now'))
            );

            CREATE UNIQUE INDEX IF NOT EXISTS idx_measurements_unique
                ON measurements(sensor_id, data_type, timestamp);

            CREATE TABLE IF NOT EXISTS analysis_results (
                id            INTEGER PRIMARY KEY AUTOINCREMENT,
                analysis_type TEXT NOT NULL,
                sensor_id     TEXT,
                zone          TEXT,
                data_type     TEXT NOT NULL,
                start_time    TEXT,
                end_time      TEXT,
                mean          REAL,
                std_dev       REAL,
                min_value     REAL,
                max_value     REAL,
                risk_level    TEXT,
                summary       TEXT,
                patterns      TEXT,
                created_at    TEXT NOT NULL DEFAULT (datetime('now'))
            );
            """;
        cmd.ExecuteNonQuery();
    }

    public SqliteConnection OpenConnection() => new(_connectionString);

    public void StoreMeasurement(string sensorId, string zone, string dataType, double value, DateTime timestamp)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT OR IGNORE INTO measurements (sensor_id, zone, data_type, value, timestamp)
            VALUES ($sid, $zone, $dt, $val, $ts)
            """;
        cmd.Parameters.AddWithValue("$sid",  sensorId);
        cmd.Parameters.AddWithValue("$zone", zone);
        cmd.Parameters.AddWithValue("$dt",   dataType);
        cmd.Parameters.AddWithValue("$val",  value);
        cmd.Parameters.AddWithValue("$ts",   timestamp.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    public int CountMeasurements(string? sensorId, string? zone, string? dataType, DateTime? from, DateTime? to)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        var conditions = new List<string>();
        if (!string.IsNullOrWhiteSpace(sensorId)) { conditions.Add("sensor_id = $sid"); cmd.Parameters.AddWithValue("$sid", sensorId); }
        if (!string.IsNullOrWhiteSpace(zone))     { conditions.Add("zone = $zone");      cmd.Parameters.AddWithValue("$zone", zone);  }
        if (!string.IsNullOrWhiteSpace(dataType)) { conditions.Add("data_type = $dt");   cmd.Parameters.AddWithValue("$dt", dataType); }
        if (from.HasValue) { conditions.Add("timestamp >= $from"); cmd.Parameters.AddWithValue("$from", from.Value.ToString("O")); }
        if (to.HasValue)   { conditions.Add("timestamp <= $to");   cmd.Parameters.AddWithValue("$to",   to.Value.ToString("O"));   }

        string where = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";
        cmd.CommandText = $"SELECT COUNT(*) FROM measurements {where}";
        return (int)(long)(cmd.ExecuteScalar() ?? 0L);
    }

    public List<MeasurementRecord> QueryMeasurements(string? sensorId, string? zone, string? dataType, DateTime? from, DateTime? to, int limit = 200, int offset = 0)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        var conditions = new List<string>();
        if (!string.IsNullOrWhiteSpace(sensorId)) { conditions.Add("sensor_id = $sid"); cmd.Parameters.AddWithValue("$sid", sensorId); }
        if (!string.IsNullOrWhiteSpace(zone))     { conditions.Add("zone = $zone");      cmd.Parameters.AddWithValue("$zone", zone);  }
        if (!string.IsNullOrWhiteSpace(dataType)) { conditions.Add("data_type = $dt");   cmd.Parameters.AddWithValue("$dt", dataType); }
        if (from.HasValue) { conditions.Add("timestamp >= $from"); cmd.Parameters.AddWithValue("$from", from.Value.ToString("O")); }
        if (to.HasValue)   { conditions.Add("timestamp <= $to");   cmd.Parameters.AddWithValue("$to",   to.Value.ToString("O"));   }
        cmd.Parameters.AddWithValue("$limit",  limit);
        cmd.Parameters.AddWithValue("$offset", offset);

        string where = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";
        cmd.CommandText = $"SELECT id, sensor_id, zone, data_type, value, timestamp, stored_at FROM measurements {where} ORDER BY timestamp DESC LIMIT $limit OFFSET $offset";

        var results = new List<MeasurementRecord>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new MeasurementRecord(
                reader.GetInt64(0), reader.GetString(1), reader.GetString(2),
                reader.GetString(3), reader.GetDouble(4), reader.GetString(5), reader.GetString(6)));
        }
        return results;
    }

    public List<MeasurementRecord> GetRecentMeasurements(int limit = 20)
        => QueryMeasurements(null, null, null, null, null, limit);

    public List<string> GetDistinctSensors()
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT DISTINCT sensor_id FROM measurements ORDER BY sensor_id";
        var result = new List<string>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) result.Add(reader.GetString(0));
        return result;
    }

    public List<string> GetDistinctDataTypes()
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT DISTINCT data_type FROM measurements ORDER BY data_type";
        var result = new List<string>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) result.Add(reader.GetString(0));
        return result;
    }

    public List<AnalysisRecord> GetAnalysisResults(int limit = 50)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, analysis_type, sensor_id, zone, data_type, mean, std_dev, min_value, max_value, risk_level, summary, patterns, created_at FROM analysis_results ORDER BY created_at DESC LIMIT $limit";
        cmd.Parameters.AddWithValue("$limit", limit);
        var results = new List<AnalysisRecord>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new AnalysisRecord(
                reader.GetInt64(0), reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.GetString(4),
                reader.IsDBNull(5) ? 0 : reader.GetDouble(5),
                reader.IsDBNull(6) ? 0 : reader.GetDouble(6),
                reader.IsDBNull(7) ? 0 : reader.GetDouble(7),
                reader.IsDBNull(8) ? 0 : reader.GetDouble(8),
                reader.IsDBNull(9) ? null : reader.GetString(9),
                reader.IsDBNull(10) ? null : reader.GetString(10),
                reader.IsDBNull(11) ? null : reader.GetString(11),
                reader.GetString(12)));
        }
        return results;
    }

    public void StoreAnalysisResult(AnalysisRecord r)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO analysis_results
              (analysis_type, sensor_id, zone, data_type, mean, std_dev, min_value, max_value, risk_level, summary, patterns)
            VALUES ($at, $sid, $zone, $dt, $mean, $std, $min, $max, $risk, $summary, $patterns)
            """;
        cmd.Parameters.AddWithValue("$at",      r.AnalysisType);
        cmd.Parameters.AddWithValue("$sid",     (object?)r.SensorId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$zone",    (object?)r.Zone ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$dt",      r.DataType);
        cmd.Parameters.AddWithValue("$mean",    r.Mean);
        cmd.Parameters.AddWithValue("$std",     r.StdDev);
        cmd.Parameters.AddWithValue("$min",     r.MinValue);
        cmd.Parameters.AddWithValue("$max",     r.MaxValue);
        cmd.Parameters.AddWithValue("$risk",    (object?)r.RiskLevel ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$summary", (object?)r.Summary ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$patterns",(object?)r.Patterns ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    public List<double> GetValuesForAnalysis(string dataType, string? sensorId, DateTime? from, DateTime? to)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        var conditions = new List<string> { "data_type = $dt" };
        cmd.Parameters.AddWithValue("$dt", dataType);
        if (!string.IsNullOrWhiteSpace(sensorId)) { conditions.Add("sensor_id = $sid"); cmd.Parameters.AddWithValue("$sid", sensorId); }
        if (from.HasValue) { conditions.Add("timestamp >= $from"); cmd.Parameters.AddWithValue("$from", from.Value.ToString("O")); }
        if (to.HasValue)   { conditions.Add("timestamp <= $to");   cmd.Parameters.AddWithValue("$to",   to.Value.ToString("O"));   }
        cmd.CommandText = $"SELECT value, timestamp FROM measurements WHERE {string.Join(" AND ", conditions)} ORDER BY timestamp ASC LIMIT 1000";
        var values = new List<double>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) values.Add(reader.GetDouble(0));
        return values;
    }
}

public record MeasurementRecord(long Id, string SensorId, string Zone, string DataType, double Value, string Timestamp, string StoredAt);
public record AnalysisRecord(long Id, string AnalysisType, string? SensorId, string? Zone, string DataType,
    double Mean, double StdDev, double MinValue, double MaxValue,
    string? RiskLevel, string? Summary, string? Patterns, string CreatedAt);
