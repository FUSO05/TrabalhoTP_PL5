using System;
using System.Collections.Generic;

namespace One_Health.Sensor;

/// <summary>
/// Dados de teste e configuração do sensor
/// </summary>
public static class SensorDefaults
{
    public static readonly string[] SupportedDataTypes = { "TEMP", "HUM", "PM2.5", "RUIDO", "AR", "LUMINOSIDADE", "PM10", "VIDEO" };
    
    // Intervalos calibrados para o Distrito de Vila Real, Portugal (clima continental, altitude ~450m)
    public static readonly Dictionary<string, (double min, double max)> DataRanges = new()
    {
        { "TEMP",        (-8,    40) },   // °C  — invernos frios, verões quentes
        { "HUM",         (30,    95) },   // %   — húmido no inverno, seco no verão
        { "PM2.5",       (2,     75) },   // μg/m³ — boa qualidade geral; picos em incêndios/poeira saáriana
        { "PM10",        (5,    120) },   // μg/m³ — idem, valores ligeiramente superiores ao PM2.5
        { "RUIDO",       (35,    90) },   // dB  — cidade média; fundo urbano 45-65 dB
        { "AR",          (0,    150) },   // IQA — geralmente bom; 150 = situação crítica pontual
        { "LUMINOSIDADE",(0, 100000) }    // lux — noite ~0; sol direto de verão ~80-100k lux (lat. 41°N)
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
