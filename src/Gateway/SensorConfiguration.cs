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

        if (line.StartsWith("sensor_id", StringComparison.OrdinalIgnoreCase))
            return null;

        var parts = line.Split(':', 5);
        if (parts.Length < 5)
            return null;

        string normalizedStatus = NormalizeStatus(parts[1].Trim());
        string typesPart = parts[3].Trim().TrimStart('[').TrimEnd(']');

        var config = new SensorConfiguration
        {
            SensorId = parts[0].Trim(),
            Status = normalizedStatus,
            Zone = parts[2].Trim(),
            SupportedDataTypes = new List<string>(typesPart.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)),
        };

        if (DateTimeOffset.TryParse(parts[4].Trim(), out var lastSync))
        {
            config.LastSync = lastSync.LocalDateTime;
        }

        return config;
    }

    private static string NormalizeStatus(string status)
    {
        string normalized = status.Trim().ToLowerInvariant();

        return normalized switch
        {
            "manutenção" => "manutencao",
            _ => normalized
        };
    }

    public string ToCsvLine()
    {
        return $"{SensorId}:{Status}:{Zone}:{string.Join(",", SupportedDataTypes)}:{LastSync:yyyy-MM-ddTHH:mm:ss.fffffff}";
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
    public int HeartbeatIntervalSeconds { get; set; } = 120;
    public bool IsRegistered { get; set; }
}
