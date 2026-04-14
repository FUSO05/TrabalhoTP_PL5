using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace One_Health.Gateway;

/// <summary>
/// Gerencia a leitura e escrita do ficheiro de configuração de sensores
/// </summary>
public class SensorConfigurationManager
{
    private readonly string filePath;
    private readonly object lockObj = new();
    private Dictionary<string, SensorConfiguration> sensors = new();

    public SensorConfigurationManager(string filePath)
    {
        this.filePath = filePath;
        LoadSensors();
    }

    private void LoadSensors()
    {
        lock (lockObj)
        {
            sensors.Clear();

            if (!File.Exists(filePath))
            {
                Console.WriteLine($"  ⚠️  Ficheiro {filePath} não encontrado");
                return;
            }

            var lines = File.ReadAllLines(filePath);
            foreach (var line in lines)
            {
                var config = SensorConfiguration.ParseFromCsv(line);
                if (config != null)
                {
                    sensors[config.SensorId] = config;
                    Console.WriteLine($"  ✓ Sensor carregado: {config.SensorId} ({config.Status}) - {config.Zone}");
                }
            }
        }
    }

    public SensorConfiguration? GetSensor(string sensorId)
    {
        lock (lockObj)
        {
            return sensors.TryGetValue(sensorId, out var config) ? config : null;
        }
    }

    public void UpdateLastSync(string sensorId)
    {
        lock (lockObj)
        {
            if (sensors.TryGetValue(sensorId, out var config))
            {
                config.LastSync = DateTime.UtcNow;
                SaveSensors();
            }
        }
    }

    public void UpdateSensorStatus(string sensorId, string status)
    {
        lock (lockObj)
        {
            if (sensors.TryGetValue(sensorId, out var config))
            {
                config.Status = status;
                config.LastSync = DateTime.UtcNow;
                SaveSensors();
            }
        }
    }

    public bool IsSensorActive(string sensorId)
    {
        lock (lockObj)
        {
            if (sensors.TryGetValue(sensorId, out var config))
            {
                return config.Status == "ativo";
            }
            return false;
        }
    }

    public bool IsSensorSupportingDataType(string sensorId, string dataType)
    {
        lock (lockObj)
        {
            if (sensors.TryGetValue(sensorId, out var config))
            {
                return config.SupportedDataTypes.Contains(dataType, StringComparer.OrdinalIgnoreCase);
            }
            return false;
        }
    }

    public string? GetSensorZone(string sensorId)
    {
        lock (lockObj)
        {
            if (sensors.TryGetValue(sensorId, out var config))
            {
                return config.Zone;
            }
            return null;
        }
    }

    private void SaveSensors()
    {
        try
        {
            var lines = sensors.Values.Select(s => s.ToCsvLine()).ToList();
            File.WriteAllLines(filePath, lines);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ Erro ao salvar configuração: {ex.Message}");
        }
    }

    public Dictionary<string, SensorConfiguration> GetAllSensors()
    {
        lock (lockObj)
        {
            return new Dictionary<string, SensorConfiguration>(sensors);
        }
    }
}
