using System;
using System.Collections.Generic;

namespace One_Health.Gateway;

/// <summary>
/// Representa a configuração de um sensor lida do CSV
/// </summary>
public class SensorConfiguration
{
    public string SensorId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // ativo, manutencao, desativado
    public string Zone { get; set; } = string.Empty;
    public List<string> SupportedDataTypes { get; set; } = new();
    public DateTime LastSync { get; set; } = DateTime.UtcNow;

    public static SensorConfiguration? ParseFromCsv(string line)
    {
        if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
            return null;

        var parts = line.Split(':');
        if (parts.Length < 5)
            return null;

        var config = new SensorConfiguration
        {
            SensorId = parts[0].Trim(),
            Status = parts[1].Trim(),
            Zone = parts[2].Trim(),
            SupportedDataTypes = new List<string>(parts[3].Split(',', StringSplitOptions.TrimEntries)),
        };

        if (DateTime.TryParse(parts[4].Trim(), out var lastSync))
        {
            config.LastSync = lastSync;
        }

        return config;
    }

    public string ToCsvLine()
    {
        return $"{SensorId}:{Status}:{Zone}:{string.Join(",", SupportedDataTypes)}:{LastSync:O}";
    }
}

/// <summary>
/// Representa um sensor conectado ao gateway
/// </summary>
public class ConnectedSensor
{
    public string SensorId { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public System.Net.Sockets.TcpClient? Client { get; set; }
    public DateTime ConnectTime { get; set; } = DateTime.UtcNow;
    public DateTime LastHeartbeat { get; set; } = DateTime.UtcNow;
    public bool IsRegistered { get; set; }
}
