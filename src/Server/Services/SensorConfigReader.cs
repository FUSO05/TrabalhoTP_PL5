namespace One_Health.Server.Services;

public record SensorConfig(string SensorId, string Status, string Zone, List<string> DataTypes);

public static class SensorConfigReader
{
    public static List<SensorConfig> Load()
    {
        string csvPath = Path.GetFullPath(
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "config", "sensors.csv"));

        if (!System.IO.File.Exists(csvPath))
            csvPath = Path.Combine(Directory.GetCurrentDirectory(), "config", "sensors.csv");

        if (!System.IO.File.Exists(csvPath)) return [];

        var result = new List<SensorConfig>();
        foreach (var line in System.IO.File.ReadLines(csvPath))
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#") ||
                line.StartsWith("sensor_id", StringComparison.OrdinalIgnoreCase))
                continue;

            var parts = line.Split(':', 5);
            if (parts.Length < 4) continue;

            string typesPart = parts[3].Trim().TrimStart('[').TrimEnd(']');
            var types = typesPart
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToList();

            result.Add(new SensorConfig(parts[0].Trim(), parts[1].Trim(), parts[2].Trim(), types));
        }
        return result;
    }
}
