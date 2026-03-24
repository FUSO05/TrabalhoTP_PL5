namespace One_Health.Common.Protocol;

/// <summary>
/// Classe base para mensagens do protocolo de comunicação SENSOR/GATEWAY/SERVIDOR
/// Baseado no diagrama de sequência definido na Fase 1
/// </summary>
public abstract class Message
{
    public string MessageType { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

// ============================================================================
// SENSOR → GATEWAY: Mensagens de Inicialização
// ============================================================================

/// <summary>
/// Mensagem 1: CONNECT
/// Sensor envia seu ID para iniciar conexão com o Gateway
/// Diagrama: CONNECT {sensor_id}
/// </summary>
public class ConnectMessage : Message
{
    public string SensorId { get; set; } = string.Empty;

    public ConnectMessage()
    {
        MessageType = "CONNECT";
    }
}

/// <summary>
/// Mensagem 2: REGISTER
/// Sensor fornece informações completas após confirmação de conexão
/// Diagrama: REGISTER {sensor_id, token, zona}
/// </summary>
public class RegisterMessage : Message
{
    public string SensorId { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string Zone { get; set; } = string.Empty;
    public List<string> SupportedDataTypes { get; set; } = new();

    public RegisterMessage()
    {
        MessageType = "REGISTER";
    }
}

// ============================================================================
// SENSOR → GATEWAY: Mensagens de Dados
// ============================================================================

/// <summary>
/// Mensagem DATA
/// Sensor envia medição ambiental
/// Diagrama: DATA {sensor_id, timestamp, tipo_dados []}
/// </summary>
public class DataMessage : Message
{
    public string SensorId { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public double Value { get; set; }

    public DataMessage()
    {
        MessageType = "DATA";
    }
}

/// <summary>
/// Mensagem HEARTBEAT
/// Sensor envia sinal de vida periodicamente
/// Diagrama: HEARTBEAT {sensor_id, timestamp}
/// </summary>
public class HeartbeatMessage : Message
{
    public string SensorId { get; set; } = string.Empty;

    public HeartbeatMessage()
    {
        MessageType = "HEARTBEAT";
    }
}

/// <summary>
/// Mensagem STREAM_REQUEST
/// Sensor requisita criação de stream de vídeo
/// Diagrama: STREAM_REQUEST {video}
/// </summary>
public class StreamRequestMessage : Message
{
    public string SensorId { get; set; } = string.Empty;
    public string StreamType { get; set; } = "VIDEO"; // Tipo de stream (VIDEO, etc)
    public string StreamEndpoint { get; set; } = string.Empty; // URL ou socket para stream

    public StreamRequestMessage()
    {
        MessageType = "STREAM_REQUEST";
    }
}

/// <summary>
/// Mensagem DISCONNECT
/// Sensor termina comunicação com o Gateway
/// Diagrama: DISCONNECT {sensor_id}
/// </summary>
public class DisconnectMessage : Message
{
    public string SensorId { get; set; } = string.Empty;

    public DisconnectMessage()
    {
        MessageType = "DISCONNECT";
    }
}

// ============================================================================
// Mensagens de Resposta (OK | ERROR)
// ============================================================================

/// <summary>
/// Mensagem de Resposta: OK
/// Enviada pelo Gateway em resposta a CONNECT, REGISTER ou DISCONNECT
/// Diagrama: OK | ERROR
/// </summary>
public class ResponseMessage : Message
{
    public string OriginalMessageType { get; set; } = string.Empty;
    public string Status { get; set; } = "OK"; // "OK" ou "ERROR"
    public string? Message { get; set; }

    public ResponseMessage()
    {
        MessageType = "RESPONSE";
    }

    public static ResponseMessage Ok(string originalMessageType, string? message = null)
    {
        return new ResponseMessage
        {
            OriginalMessageType = originalMessageType,
            Status = "OK",
            Message = message
        };
    }

    public static ResponseMessage Error(string originalMessageType, string errorMessage)
    {
        return new ResponseMessage
        {
            OriginalMessageType = originalMessageType,
            Status = "ERROR",
            Message = errorMessage
        };
    }
}

// ============================================================================
// GATEWAY → SERVIDOR: Mensagens de Armazenamento
// ============================================================================

/// <summary>
/// Mensagem STORE
/// Gateway envia dados validados para o Servidor
/// Diagrama: STORE {dados validados}
/// </summary>
public class StoreMessage : Message
{
    public string SensorId { get; set; } = string.Empty;
    public string Zone { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public double Value { get; set; }

    public StoreMessage()
    {
        MessageType = "STORE";
    }
}

// ============================================================================
// SERVIDOR → GATEWAY: Mensagens de Confirmação de Armazenamento
// ============================================================================

/// <summary>
/// Mensagem de Resposta: STORED | ERROR
/// Enviada pelo Servidor após receber dados
/// Diagrama: STORED | ERROR
/// </summary>
public class StorageResponseMessage : Message
{
    public string Status { get; set; } = "STORED"; // "STORED" ou "ERROR"
    public string? Message { get; set; }

    public StorageResponseMessage()
    {
        MessageType = "STORAGE_RESPONSE";
    }

    public static StorageResponseMessage Stored(string? message = null)
    {
        return new StorageResponseMessage
        {
            Status = "STORED",
            Message = message
        };
    }

    public static StorageResponseMessage Error(string errorMessage)
    {
        return new StorageResponseMessage
        {
            Status = "ERROR",
            Message = errorMessage
        };
    }
}
