using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace One_Health.Gateway;

/// <summary>
/// Gerencia a leitura e escrita do ficheiro de configuração de sensores
/// </summary>
public class SensorConfigurationManager
{
    private readonly string filePath;
    private readonly Mutex fileMutex = new();
    private Dictionary<string, SensorConfiguration> sensors = new();

    public SensorConfigurationManager(string filePath)
    {
        this.filePath = filePath;
        LoadSensors();
    }

    private void LoadSensors()
    {
        fileMutex.WaitOne();
        try
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

            NormalizeAllLastSyncToLocalTime();
        }
        finally
        {
            fileMutex.ReleaseMutex();
        }
    }

    public SensorConfiguration? GetSensor(string sensorId)
    {
        fileMutex.WaitOne();
        try
        {
            return sensors.TryGetValue(sensorId, out var config) ? config : null;
        }
        finally
        {
            fileMutex.ReleaseMutex();
        }
    }

    private void NormalizeAllLastSyncToLocalTime()
    {
        bool changed = false;

        foreach (var sensor in sensors.Values)
        {
            DateTime normalized = sensor.LastSync.Kind switch
            {
                DateTimeKind.Utc => sensor.LastSync.ToLocalTime(),
                DateTimeKind.Local => sensor.LastSync,
                _ => DateTime.SpecifyKind(sensor.LastSync, DateTimeKind.Local)
            };

            if (normalized != sensor.LastSync)
            {
                sensor.LastSync = normalized;
                changed = true;
            }
        }

        if (changed)
        {
            SaveSensors();
        }
    }

    public void UpdateLastSync(string sensorId)
    {
        fileMutex.WaitOne();
        try
        {
            if (sensors.TryGetValue(sensorId, out var config))
            {
                config.LastSync = DateTime.Now;
                SaveSensors();
            }
        }
        finally
        {
            fileMutex.ReleaseMutex();
        }
    }

    public void UpdateSensorStatus(string sensorId, string status)
    {
        fileMutex.WaitOne();
        try
        {
            if (sensors.TryGetValue(sensorId, out var config))
            {
                config.Status = status;
                config.LastSync = DateTime.Now;
                SaveSensors();
            }
        }
        finally
        {
            fileMutex.ReleaseMutex();
        }
    }

    public bool IsSensorActive(string sensorId)
    {
        fileMutex.WaitOne();
        try
        {
            if (sensors.TryGetValue(sensorId, out var config))
            {
                return config.Status == "ativo";
            }
            return false;
        }
        finally
        {
            fileMutex.ReleaseMutex();
        }
    }

    public bool IsSensorSupportingDataType(string sensorId, string dataType)
    {
        fileMutex.WaitOne();
        try
        {
            if (sensors.TryGetValue(sensorId, out var config))
            {
                return config.SupportedDataTypes.Contains(dataType, StringComparer.OrdinalIgnoreCase);
            }
            return false;
        }
        finally
        {
            fileMutex.ReleaseMutex();
        }
    }

    public string? GetSensorZone(string sensorId)
    {
        fileMutex.WaitOne();
        try
        {
            if (sensors.TryGetValue(sensorId, out var config))
            {
                return config.Zone;
            }
            return null;
        }
        finally
        {
            fileMutex.ReleaseMutex();
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
        fileMutex.WaitOne();
        try
        {
            return new Dictionary<string, SensorConfiguration>(sensors);
        }
        finally
        {
            fileMutex.ReleaseMutex();
        }
    }
}
