namespace One_Health.Common.Models;

/// <summary>
/// Representa um tipo de dado ambiental suportado
/// </summary>
public enum EnvironmentalDataType
{
    TEMP,      // Temperatura
    HUM,       // Humidade
    PM2_5,     // Partículas PM2.5
    PM10,      // Partículas PM10
    RUIDO,     // Nível de Ruído
    AR,        // Qualidade do Ar
    LUMINOSIDADE,  // Luminosidade
    VIDEO      // Stream de vídeo
}

/// <summary>
/// Representa uma zona da cidade
/// </summary>
public enum CityZone
{
    ZONA_CENTRO,
    ZONA_ESCOLAR,
    ZONA_INDUSTRIAL,
    ZONA_RESIDENCIAL,
    ZONA_PARQUE
}

/// <summary>
/// Estados possíveis de um sensor
/// </summary>
public enum SensorStatus
{
    Ativo,       // Sensor em funcionamento normal
    Manutencao,  // Sensor temporariamente indisponível
    Desativado   // Sensor removido ou desligado
}

/// <summary>
/// Informações básicas de um sensor
/// </summary>
public class Sensor
{
    public string SensorId { get; set; } = string.Empty;
    public SensorStatus Status { get; set; }
    public CityZone Zone { get; set; }
    public List<EnvironmentalDataType> SupportedDataTypes { get; set; } = new();
    public DateTime LastSync { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Representa uma medição ambiental
/// </summary>
public class EnvironmentalMeasurement
{
    public string SensorId { get; set; } = string.Empty;
    public CityZone Zone { get; set; }
    public EnvironmentalDataType DataType { get; set; }
    public double Value { get; set; }
    public DateTime MeasurementTime { get; set; } = DateTime.UtcNow;
}
