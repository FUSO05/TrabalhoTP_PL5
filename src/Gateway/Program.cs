using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using One_Health.Common.Protocol;
using One_Health.Common.Utilities;

namespace One_Health.Gateway;

/// <summary>
/// GATEWAY: Recebe dados dos SENSORES, valida e roteia para o SERVIDOR
/// </summary>
internal class GatewayProgram
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);
    private static TcpListener? listener;
    private static TcpClient? serverConnection;
    private static int serverPort = 8000;
    private static string serverIp = "127.0.0.1";
    private static SensorConfigurationManager? configManager;
    private static Dictionary<string, ConnectedSensor> connectedSensors = new();
    private static Dictionary<string, StreamSession> activeStreams = new();
    private static readonly Mutex sensorMutex = new();
    private static readonly Mutex serverWriteMutex = new();
    private static readonly Mutex streamFileMutex = new();

    static void Main(string[] args)
    {
        Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║   ONE HEALTH GATEWAY                                       ║");
        Console.WriteLine("║   Agregação e Encaminhamento de Dados                      ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        int port = args.Length > 0 ? int.Parse(args[0]) : 9000;
        string configFile = args.Length > 1 ? args[1] : "config/sensors.csv";
        serverIp = args.Length > 2 ? args[2] : "127.0.0.1";
        serverPort = args.Length > 3 ? int.Parse(args[3]) : 8000;

        Console.WriteLine($"📡 Porta de escuta: {port}");
        Console.WriteLine($"📁 Ficheiro de configuração: {configFile}");
        Console.WriteLine($"🔗 Servidor: {serverIp}:{serverPort}");
        Console.WriteLine();

        // Carregar configuração de sensores
        configManager = new SensorConfigurationManager(configFile);
        Console.WriteLine();

        // Conectar ao servidor
        if (!ConnectToServer())
        {
            Console.WriteLine("❌ Não foi possível conectar ao servidor. Verifique se o servidor está rodando.");
            return;
        }

        try
        {
            listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            Console.WriteLine($"✓ Gateway iniciado na porta {port}");
            Console.WriteLine("⏳ Aguardando conexões de sensores...\n");

            // Thread para monitorar heartbeat timeout
            Thread monitorThread = new(MonitorHeartbeatTimeout)
            {
                IsBackground = true
            };
            monitorThread.Start();

            // Loop de aceitação de conexões
            while (true)
            {
                TcpClient client = listener.AcceptTcpClient();
                Console.WriteLine($"→ Conexão recebida de {client.Client.RemoteEndPoint}");

                Thread clientThread = new(() => HandleClient(client))
                {
                    IsBackground = true
                };
                clientThread.Start();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro: {ex.Message}");
        }
        finally
        {
            listener?.Stop();
            serverConnection?.Close();
        }
    }

    private static bool ConnectToServer()
    {
        try
        {
            serverConnection = new TcpClient();
            serverConnection.Connect(serverIp, serverPort);
            Console.WriteLine($"✓ Conectado ao servidor {serverIp}:{serverPort}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro ao conectar ao servidor: {ex.Message}");
            return false;
        }
    }

    private static void HandleClient(TcpClient client)
    {
        string sensorId = "DESCONHECIDO";
        try
        {
            using (NetworkStream stream = client.GetStream())
            using (StreamReader reader = new(stream, Encoding.UTF8))
            using (StreamWriter writer = new(stream, Encoding.UTF8) { AutoFlush = true })
            {
                string? line;
                while ((line = reader.ReadLine()) != null && line.Length > 0)
                {
                    Console.WriteLine($"  📨 Mensagem recebida: {line.Substring(0, Math.Min(80, line.Length))}...");

                    ProcessMessage(line, writer, ref sensorId, client);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ Erro ao processar cliente: {ex.Message}");
        }
        finally
        {
            // Remover sensor conectado
            if (!string.IsNullOrEmpty(sensorId))
            {
                sensorMutex.WaitOne();
                try
                {
                    if (connectedSensors.TryGetValue(sensorId, out var sensor))
                    {
                        connectedSensors.Remove(sensorId);
                        Console.WriteLine($"  × Sensor {sensorId} desconectado");
                    }
                }
                finally
                {
                    sensorMutex.ReleaseMutex();
                }
            }
            client.Close();
            Console.WriteLine($"← Conexão fechada\n");
        }
    }

    private static void ProcessMessage(string json, StreamWriter writer, ref string sensorId, TcpClient client)
    {
        try
        {
            var baseMessage = MessageFactory.DeserializeMessage(json);

            if (baseMessage == null)
            {
                SendResponse(writer, ResponseMessage.Error("UNKNOWN", "Mensagem inválida"));
                return;
            }

            switch (baseMessage.MessageType)
            {
                case "CONNECT":
                    HandleConnectMessage(json, writer, ref sensorId, client);
                    break;

                case "REGISTER":
                    HandleRegisterMessage(json, writer, sensorId);
                    break;

                case "DATA":
                    HandleDataMessage(json, writer, sensorId);
                    break;

                case "HEARTBEAT":
                    HandleHeartbeatMessage(json, writer, sensorId);
                    break;

                case "STREAM_REQUEST":
                    HandleStreamRequestMessage(json, writer, sensorId);
                    break;

                case "STREAM_START":
                    HandleStreamStartMessage(json, writer, sensorId);
                    break;

                case "STREAM_FRAME":
                    HandleStreamFrameMessage(json, writer, sensorId);
                    break;

                case "STREAM_END":
                    HandleStreamEndMessage(json, writer, sensorId);
                    break;

                case "DISCONNECT":
                    HandleDisconnectMessage(json, writer, sensorId);
                    break;

                default:
                    Console.WriteLine($"  ⚠️  Tipo de mensagem não suportado: {baseMessage.MessageType}");
                    SendResponse(writer, ResponseMessage.Error(baseMessage.MessageType, "Tipo de mensagem não suportado"));
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ Erro ao processar mensagem: {ex.Message}");
            SendResponse(writer, ResponseMessage.Error("UNKNOWN", $"Erro ao processar mensagem: {ex.Message}"));
        }
    }

    private static void HandleConnectMessage(string json, StreamWriter writer, ref string sensorId, TcpClient client)
    {
        var msg = MessageSerializer.Deserialize<ConnectMessage>(json);
        if (msg == null)
        {
            SendResponse(writer, ResponseMessage.Error("CONNECT", "Mensagem CONNECT inválida"));
            return;
        }

        sensorId = msg.SensorId;
        Console.WriteLine($"  🔗 CONNECT: {sensorId}");

        // Verificar se sensor está registado
        var sensorConfig = configManager?.GetSensor(sensorId);
        if (sensorConfig == null)
        {
            Console.WriteLine($"  ❌ Sensor {sensorId} não encontrado em configuração");
            SendResponse(writer, ResponseMessage.Error("CONNECT", $"Sensor {sensorId} não está registado"));
            return;
        }

        // Gerar token
        string token = GenerateToken(sensorId);
        Console.WriteLine($"  ✓ Token gerado: {token}");

        // Adicionar sensor conectado
        sensorMutex.WaitOne();
        try
        {
            connectedSensors[sensorId] = new ConnectedSensor
            {
                SensorId = sensorId,
                Token = token,
                Client = client,
                ConnectTime = DateTime.UtcNow,
                LastHeartbeat = DateTime.UtcNow,
                HeartbeatIntervalSeconds = 120,
                IsRegistered = false
            };
        }
        finally
        {
            sensorMutex.ReleaseMutex();
        }

        // Responder com OK (incluir token na mensagem)
        var response = new ResponseMessage
        {
            OriginalMessageType = "CONNECT",
            Status = "OK",
            Message = token // Token é enviado na mensagem
        };
        SendResponse(writer, response);
    }

    private static void HandleRegisterMessage(string json, StreamWriter writer, string sensorId)
    {
        var msg = MessageSerializer.Deserialize<RegisterMessage>(json);
        if (msg == null)
        {
            SendResponse(writer, ResponseMessage.Error("REGISTER", "Mensagem REGISTER inválida"));
            return;
        }

        Console.WriteLine($"  📝 REGISTER: {sensorId}");

        // Validar sensor
        var sensorConfig = configManager?.GetSensor(sensorId);
        if (sensorConfig == null)
        {
            SendResponse(writer, ResponseMessage.Error("REGISTER", "Sensor não registado"));
            return;
        }

        // Verificar status
        if (sensorConfig.Status != "ativo")
        {
            SendResponse(writer, ResponseMessage.Error("REGISTER", $"Sensor em estado: {sensorConfig.Status}"));
            return;
        }

        // Verificar zona configurada
        if (!string.Equals(msg.Zone, sensorConfig.Zone, StringComparison.OrdinalIgnoreCase))
        {
            SendResponse(writer, ResponseMessage.Error("REGISTER", "Zona inválida para este sensor"));
            return;
        }

        // Verificar tipos de dados suportados
        var configuredTypes = sensorConfig.SupportedDataTypes;
        bool registerTypesValid = msg.SupportedDataTypes != null
            && msg.SupportedDataTypes.Count > 0
            && msg.SupportedDataTypes.All(type => configuredTypes.Contains(type, StringComparer.OrdinalIgnoreCase));

        if (!registerTypesValid)
        {
            SendResponse(writer, ResponseMessage.Error("REGISTER", "Tipos de dados inválidos para este sensor"));
            return;
        }

        // Verificar token
        sensorMutex.WaitOne();
        try
        {
            if (!connectedSensors.TryGetValue(sensorId, out var sensor) || sensor.Token != msg.Token)
            {
                SendResponse(writer, ResponseMessage.Error("REGISTER", "Token inválido"));
                return;
            }

            // Validar zona
            if (sensor.Client != null)
            {
                sensor.IsRegistered = true;
            }
        }
        finally
        {
            sensorMutex.ReleaseMutex();
        }

        // Atualizar last_sync
        configManager?.UpdateLastSync(sensorId);
        Console.WriteLine($"  ✓ Sensor {sensorId} registado com sucesso");

        SendResponse(writer, ResponseMessage.Ok("REGISTER", "Sensor registado com sucesso"));
    }

    private static void HandleDataMessage(string json, StreamWriter writer, string sensorId)
    {
        var msg = MessageSerializer.Deserialize<DataMessage>(json);
        if (msg == null)
        {
            SendResponse(writer, ResponseMessage.Error("DATA", "Mensagem DATA inválida"));
            return;
        }

        Console.WriteLine($"  📊 DATA: {sensorId} - {msg.DataType}={msg.Value}");

        // Validar sensor registado
        sensorMutex.WaitOne();
        try
        {
            if (!connectedSensors.TryGetValue(sensorId, out var sensor) || !sensor.IsRegistered)
            {
                SendResponse(writer, ResponseMessage.Error("DATA", "Sensor não está registado"));
                return;
            }
        }
        finally
        {
            sensorMutex.ReleaseMutex();
        }

        // Validar tipo de dado suportado
        if (!configManager?.IsSensorSupportingDataType(sensorId, msg.DataType) ?? false)
        {
            Console.WriteLine($"  ❌ Tipo de dado {msg.DataType} não suportado por {sensorId}");
            SendResponse(writer, ResponseMessage.Error("DATA", $"Tipo de dado {msg.DataType} não suportado"));
            return;
        }

        // Encaminhar para servidor
        bool forwarded = ForwardToServer(sensorId, msg);
        if (!forwarded)
        {
            SendResponse(writer, ResponseMessage.Error("DATA", "Falha ao encaminhar dados para o servidor"));
            return;
        }

        // Atualizar last_sync
        configManager?.UpdateLastSync(sensorId);
        Console.WriteLine($"  ✓ Dados encaminhados para servidor");
        SendResponse(writer, ResponseMessage.Ok("DATA", "Dados recebidos e encaminhados com sucesso"));
    }

    private static void HandleHeartbeatMessage(string json, StreamWriter writer, string sensorId)
    {
        var msg = MessageSerializer.Deserialize<HeartbeatMessage>(json);
        if (msg == null)
        {
            SendResponse(writer, ResponseMessage.Error("HEARTBEAT", "Mensagem HEARTBEAT inválida"));
            return;
        }

        Console.WriteLine($"  💓 HEARTBEAT: {sensorId}");

        // Atualizar last sync
        sensorMutex.WaitOne();
        try
        {
            if (connectedSensors.TryGetValue(sensorId, out var sensor))
            {
                sensor.LastHeartbeat = DateTime.UtcNow;
                sensor.HeartbeatIntervalSeconds = GetEffectiveHeartbeatIntervalSeconds(msg);
            }
        }
        finally
        {
            sensorMutex.ReleaseMutex();
        }

        configManager?.UpdateLastSync(sensorId);
    }

    private static void HandleStreamRequestMessage(string json, StreamWriter writer, string sensorId)
    {
        var msg = MessageSerializer.Deserialize<StreamRequestMessage>(json);
        if (msg == null)
        {
            SendResponse(writer, ResponseMessage.Error("STREAM_REQUEST", "Mensagem STREAM_REQUEST inválida"));
            return;
        }

        if (!IsSensorRegistered(sensorId))
        {
            SendResponse(writer, ResponseMessage.Error("STREAM_REQUEST", "Sensor não está registado"));
            return;
        }

        Console.WriteLine($"  📹 STREAM_REQUEST: {sensorId} - {msg.StreamType}");
        SendResponse(writer, ResponseMessage.Ok("STREAM_REQUEST", "Stream request recebido"));
    }

    private static void HandleStreamStartMessage(string json, StreamWriter writer, string sensorId)
    {
        var msg = MessageSerializer.Deserialize<StreamStartMessage>(json);
        if (msg == null || string.IsNullOrWhiteSpace(msg.StreamId))
        {
            SendResponse(writer, ResponseMessage.Error("STREAM_START", "Mensagem STREAM_START inválida"));
            return;
        }

        if (!IsSensorRegistered(sensorId))
        {
            SendResponse(writer, ResponseMessage.Error("STREAM_START", "Sensor não está registado"));
            return;
        }

        sensorMutex.WaitOne();
        try
        {
            activeStreams[msg.StreamId] = new StreamSession
            {
                StreamId = msg.StreamId,
                SensorId = sensorId,
                StreamType = msg.StreamType,
                StartTime = DateTime.Now,
                LastFrameTime = DateTime.Now,
                FrameCount = 0
            };
        }
        finally
        {
            sensorMutex.ReleaseMutex();
        }

        Console.WriteLine($"  ▶️ STREAM_START: Sensor={sensorId}, StreamId={msg.StreamId}, Tipo={msg.StreamType}");
        SendResponse(writer, ResponseMessage.Ok("STREAM_START", "Stream iniciada"));
    }

    private static void HandleStreamFrameMessage(string json, StreamWriter writer, string sensorId)
    {
        var msg = MessageSerializer.Deserialize<StreamFrameMessage>(json);
        if (msg == null || string.IsNullOrWhiteSpace(msg.StreamId))
        {
            SendResponse(writer, ResponseMessage.Error("STREAM_FRAME", "Mensagem STREAM_FRAME inválida"));
            return;
        }

        if (!IsSensorRegistered(sensorId))
        {
            SendResponse(writer, ResponseMessage.Error("STREAM_FRAME", "Sensor não está registado"));
            return;
        }

        sensorMutex.WaitOne();
        try
        {
            if (!activeStreams.TryGetValue(msg.StreamId, out var session) || session.SensorId != sensorId)
            {
                SendResponse(writer, ResponseMessage.Error("STREAM_FRAME", "Stream não iniciada"));
                return;
            }

            session.FrameCount++;
            session.LastFrameTime = DateTime.Now;
            session.LastSequence = msg.Sequence;
        }
        finally
        {
            sensorMutex.ReleaseMutex();
        }

        string streamsDir = Path.Combine("data", "streams");
        streamFileMutex.WaitOne();
        try
        {
            Directory.CreateDirectory(streamsDir);
            string streamLogFile = Path.Combine(streamsDir, $"stream_{msg.StreamId}.log");
            string logLine = $"{DateTime.Now:yyyy-MM-ddTHH:mm:ss.fffffff}:{sensorId}:{msg.StreamId}:{msg.Sequence}:{msg.PayloadBase64.Length}";
            File.AppendAllText(streamLogFile, logLine + Environment.NewLine);
        }
        finally
        {
            streamFileMutex.ReleaseMutex();
        }

        SendResponse(writer, ResponseMessage.Ok("STREAM_FRAME", "Frame recebido"));
    }

    private static void HandleStreamEndMessage(string json, StreamWriter writer, string sensorId)
    {
        var msg = MessageSerializer.Deserialize<StreamEndMessage>(json);
        if (msg == null || string.IsNullOrWhiteSpace(msg.StreamId))
        {
            SendResponse(writer, ResponseMessage.Error("STREAM_END", "Mensagem STREAM_END inválida"));
            return;
        }

        if (!IsSensorRegistered(sensorId))
        {
            SendResponse(writer, ResponseMessage.Error("STREAM_END", "Sensor não está registado"));
            return;
        }

        StreamSession? session = null;
        sensorMutex.WaitOne();
        try
        {
            if (activeStreams.TryGetValue(msg.StreamId, out var existing) && existing.SensorId == sensorId)
            {
                session = existing;
                activeStreams.Remove(msg.StreamId);
            }
        }
        finally
        {
            sensorMutex.ReleaseMutex();
        }

        if (session == null)
        {
            SendResponse(writer, ResponseMessage.Error("STREAM_END", "Stream não encontrada"));
            return;
        }

        Console.WriteLine($"  ⏹️ STREAM_END: Sensor={sensorId}, StreamId={msg.StreamId}, Frames={session.FrameCount}");
        SendResponse(writer, ResponseMessage.Ok("STREAM_END", $"Stream finalizada com {session.FrameCount} frames"));
    }

    private static void HandleDisconnectMessage(string json, StreamWriter writer, string sensorId)
    {
        var msg = MessageSerializer.Deserialize<DisconnectMessage>(json);
        if (msg == null)
        {
            SendResponse(writer, ResponseMessage.Error("DISCONNECT", "Mensagem DISCONNECT inválida"));
            return;
        }

        Console.WriteLine($"  🔌 DISCONNECT: {sensorId}");

        sensorMutex.WaitOne();
        try
        {
            connectedSensors.Remove(sensorId);
        }
        finally
        {
            sensorMutex.ReleaseMutex();
        }

        SendResponse(writer, ResponseMessage.Ok("DISCONNECT", "Desconexão confirmada"));
    }

internal class StreamSession
{
    public string StreamId { get; set; } = string.Empty;
    public string SensorId { get; set; } = string.Empty;
    public string StreamType { get; set; } = "VIDEO";
    public DateTime StartTime { get; set; }
    public DateTime LastFrameTime { get; set; }
    public int FrameCount { get; set; }
    public int LastSequence { get; set; }
}

    private static bool IsSensorRegistered(string sensorId)
    {
        sensorMutex.WaitOne();
        try
        {
            return connectedSensors.TryGetValue(sensorId, out var sensor) && sensor.IsRegistered;
        }
        finally
        {
            sensorMutex.ReleaseMutex();
        }
    }

    private static bool ForwardToServer(string sensorId, DataMessage data)
    {
        try
        {
            if (serverConnection == null || !serverConnection.Connected)
            {
                Console.WriteLine("  ❌ Conexão com servidor perdida");
                return false;
            }

            var zone = configManager?.GetSensorZone(sensorId);
            var storeMsg = new StoreMessage
            {
                SensorId = sensorId,
                Zone = zone ?? "DESCONHECIDA",
                DataType = data.DataType,
                Value = data.Value,
                Timestamp = data.Timestamp
            };

            string json = MessageSerializer.Serialize(storeMsg);

            serverWriteMutex.WaitOne();
            try
            {
                NetworkStream stream = serverConnection.GetStream();

                // leaveOpen = true para NÃO fechar a conexão TCP com o servidor
                using StreamWriter writer = new(stream, Utf8NoBom, 1024, leaveOpen: true)
                {
                    AutoFlush = true
                };

                writer.WriteLine(json);

                using StreamReader reader = new(stream, Utf8NoBom, false, 1024, leaveOpen: true);
                string? responseLine = reader.ReadLine();

                if (string.IsNullOrWhiteSpace(responseLine))
                {
                    Console.WriteLine("  ⚠️  Sem resposta de armazenamento do servidor");
                    return false;
                }

                responseLine = responseLine.TrimStart('\uFEFF');

                var storageResponse = MessageFactory.DeserializeMessage(responseLine) as StorageResponseMessage;
                storageResponse ??= MessageSerializer.Deserialize<StorageResponseMessage>(responseLine);
                if (storageResponse == null || storageResponse.Status != "STORED")
                {
                    Console.WriteLine($"  ❌ Servidor rejeitou armazenamento: {storageResponse?.Message ?? "resposta inválida"}");
                    return false;
                }
            }
            finally
            {
                serverWriteMutex.ReleaseMutex();
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ Erro ao encaminhar para servidor: {ex.Message}");
            return false;
        }
    }

    private static void SendResponse(StreamWriter writer, ResponseMessage response)
    {
        string json = MessageSerializer.Serialize(response);
        writer.WriteLine(json);
    }

    private static string GenerateToken(string sensorId)
    {
        // Token simples: Base64(sensorId + timestamp)
        string data = $"{sensorId}_{DateTime.UtcNow.Ticks}";
        byte[] bytes = Encoding.UTF8.GetBytes(data);
        return Convert.ToBase64String(bytes);
    }

    private static void MonitorHeartbeatTimeout()
    {
        while (true)
        {
            Thread.Sleep(10000); // Verificar a cada 10 segundos

            sensorMutex.WaitOne();
            try
            {
                var now = DateTime.UtcNow;
                var timedOutSensors = connectedSensors
                    .Where(kvp =>
                    {
                        int timeoutSeconds = kvp.Value.HeartbeatIntervalSeconds * 3;
                        return (now - kvp.Value.LastHeartbeat).TotalSeconds > timeoutSeconds;
                    })
                    .ToList();

                foreach (var kvp in timedOutSensors)
                {
                    Console.WriteLine($"  ⏱️  Sensor {kvp.Key} timeout (sem heartbeat)");
                    kvp.Value.Client?.Close();
                    connectedSensors.Remove(kvp.Key);
                }
            }
            finally
            {
                sensorMutex.ReleaseMutex();
            }
        }
    }

    private static int GetEffectiveHeartbeatIntervalSeconds(HeartbeatMessage msg)
    {
        if (msg.IsLowBattery)
        {
            return 300;
        }

        if (msg.IsStreaming)
        {
            return 20;
        }

        if (msg.IntervalSeconds <= 0)
        {
            return 120;
        }

        return msg.IntervalSeconds;
    }
}
