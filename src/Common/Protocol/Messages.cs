using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace One_Health.Common.Protocol;

/// <summary>
/// Envelope temporário para desserialização polimórfica de mensagens
/// Extrai o messageType sem desserializar o objeto completo
/// </summary>
internal class MessageEnvelope
{
    [JsonPropertyName("messageType")]
    public string? MessageType { get; set; }
}

/// <summary>
/// Factory para criar mensagens concretas a partir de JSON
/// Resolve o problema de desserialização de tipos abstratos
/// </summary>
public static class MessageFactory
{
    public static Message? DeserializeMessage(string json)
    {
        try
        {
            // Primeiro, extrai apenas o messageType
            var envelope = JsonSerializer.Deserialize<MessageEnvelope>(json, 
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (string.IsNullOrEmpty(envelope?.MessageType))
                return null;

            // Depois, desserializa para o tipo concreto apropriado
            return envelope.MessageType switch
            {
                "CONNECT" => JsonSerializer.Deserialize<ConnectMessage>(json, 
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                "REGISTER" => JsonSerializer.Deserialize<RegisterMessage>(json, 
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                "DATA" => JsonSerializer.Deserialize<DataMessage>(json, 
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                "HEARTBEAT" => JsonSerializer.Deserialize<HeartbeatMessage>(json, 
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                "STREAM_REQUEST" => JsonSerializer.Deserialize<StreamRequestMessage>(json, 
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                "STREAM_START" => JsonSerializer.Deserialize<StreamStartMessage>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                "STREAM_FRAME" => JsonSerializer.Deserialize<StreamFrameMessage>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                "STREAM_END" => JsonSerializer.Deserialize<StreamEndMessage>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                "DISCONNECT" => JsonSerializer.Deserialize<DisconnectMessage>(json, 
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                "RESPONSE" => JsonSerializer.Deserialize<ResponseMessage>(json, 
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                "STORE" => JsonSerializer.Deserialize<StoreMessage>(json, 
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                "STORAGE_RESPONSE" => JsonSerializer.Deserialize<StorageResponseMessage>(json, 
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// Mensagem STREAM_START
/// Sensor inicia o envio lógico de uma stream
/// </summary>
public class StreamStartMessage : Message
{
    public string SensorId { get; set; } = string.Empty;
    public string StreamId { get; set; } = string.Empty;
    public string StreamType { get; set; } = "VIDEO";

    public StreamStartMessage()
    {
        MessageType = "STREAM_START";
    }
}

/// <summary>
/// Mensagem STREAM_FRAME
/// Sensor envia um frame codificado em Base64
/// </summary>
public class StreamFrameMessage : Message
{
    public string SensorId { get; set; } = string.Empty;
    public string StreamId { get; set; } = string.Empty;
    public int Sequence { get; set; }
    public string PayloadBase64 { get; set; } = string.Empty;

    public StreamFrameMessage()
    {
        MessageType = "STREAM_FRAME";
    }
}

/// <summary>
/// Mensagem STREAM_END
/// Sensor termina o envio lógico da stream
/// </summary>
public class StreamEndMessage : Message
{
    public string SensorId { get; set; } = string.Empty;
    public string StreamId { get; set; } = string.Empty;
    public string? Reason { get; set; }

    public StreamEndMessage()
    {
        MessageType = "STREAM_END";
    }
}

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
    public int IntervalSeconds { get; set; } = 120;
    public bool IsStreaming { get; set; }
    public bool IsLowBattery { get; set; }

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
