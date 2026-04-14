using System;
using System.Collections.Generic;

namespace One_Health.Sensor;

/// <summary>
/// Dados de teste e configuração do sensor
/// </summary>
public static class SensorDefaults
{
    public static readonly string[] SupportedDataTypes = { "TEMP", "HUM", "PM2.5", "RUIDO", "AR", "LUMINOSIDADE", "PM10", "VIDEO" };
    
    public static readonly Dictionary<string, (double min, double max)> DataRanges = new()
    {
        { "TEMP", (-50, 60) },
        { "HUM", (0, 100) },
        { "PM2.5", (0, 500) },
        { "PM10", (0, 500) },
        { "RUIDO", (0, 150) },
        { "AR", (0, 500) },
        { "LUMINOSIDADE", (0, 150000) }
    };

    public static double GetRandomValue(string dataType)
    {
        var random = new Random();
        if (DataRanges.TryGetValue(dataType, out var range))
        {
            return Math.Round(random.NextDouble() * (range.max - range.min) + range.min, 2);
        }
        return 0;
    }
}
