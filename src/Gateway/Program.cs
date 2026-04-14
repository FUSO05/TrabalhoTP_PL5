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
    private static TcpListener? listener;
    private static TcpClient? serverConnection;
    private static int serverPort = 8000;
    private static string serverIp = "127.0.0.1";
    private static SensorConfigurationManager? configManager;
    private static Dictionary<string, ConnectedSensor> connectedSensors = new();
    private static readonly object sensorLock = new();

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
                lock (sensorLock)
                {
                    if (connectedSensors.TryGetValue(sensorId, out var sensor))
                    {
                        connectedSensors.Remove(sensorId);
                        Console.WriteLine($"  × Sensor {sensorId} desconectado");
                    }
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
            var baseMessage = MessageSerializer.Deserialize<Message>(json);

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
        lock (sensorLock)
        {
            connectedSensors[sensorId] = new ConnectedSensor
            {
                SensorId = sensorId,
                Token = token,
                Client = client,
                ConnectTime = DateTime.UtcNow,
                LastHeartbeat = DateTime.UtcNow,
                IsRegistered = false
            };
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

        // Verificar token
        lock (sensorLock)
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
        lock (sensorLock)
        {
            if (!connectedSensors.TryGetValue(sensorId, out var sensor) || !sensor.IsRegistered)
            {
                SendResponse(writer, ResponseMessage.Error("DATA", "Sensor não está registado"));
                return;
            }
        }

        // Validar tipo de dado suportado
        if (!configManager?.IsSensorSupportingDataType(sensorId, msg.DataType) ?? false)
        {
            Console.WriteLine($"  ❌ Tipo de dado {msg.DataType} não suportado por {sensorId}");
            SendResponse(writer, ResponseMessage.Error("DATA", $"Tipo de dado {msg.DataType} não suportado"));
            return;
        }

        // Encaminhar para servidor
        ForwardToServer(sensorId, msg);

        // Atualizar last_sync
        configManager?.UpdateLastSync(sensorId);
        Console.WriteLine($"  ✓ Dados encaminhados para servidor");
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
        lock (sensorLock)
        {
            if (connectedSensors.TryGetValue(sensorId, out var sensor))
            {
                sensor.LastHeartbeat = DateTime.UtcNow;
            }
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

        Console.WriteLine($"  📹 STREAM_REQUEST: {sensorId} - {msg.StreamType}");
        SendResponse(writer, ResponseMessage.Ok("STREAM_REQUEST", "Stream request recebido"));
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

        lock (sensorLock)
        {
            connectedSensors.Remove(sensorId);
        }

        SendResponse(writer, ResponseMessage.Ok("DISCONNECT", "Desconexão confirmada"));
    }

    private static void ForwardToServer(string sensorId, DataMessage data)
    {
        try
        {
            if (serverConnection == null || !serverConnection.Connected)
            {
                Console.WriteLine($"  ❌ Conexão com servidor perdida");
                return;
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

            using (NetworkStream stream = serverConnection.GetStream())
            using (StreamWriter writer = new(stream, Encoding.UTF8) { AutoFlush = true })
            {
                writer.WriteLine(json);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ Erro ao encaminhar para servidor: {ex.Message}");
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
        const int heartbeatTimeoutSeconds = 30;

        while (true)
        {
            Thread.Sleep(10000); // Verificar a cada 10 segundos

            lock (sensorLock)
            {
                var now = DateTime.UtcNow;
                var timedOutSensors = connectedSensors
                    .Where(kvp => (now - kvp.Value.LastHeartbeat).TotalSeconds > heartbeatTimeoutSeconds)
                    .ToList();

                foreach (var kvp in timedOutSensors)
                {
                    Console.WriteLine($"  ⏱️  Sensor {kvp.Key} timeout (sem heartbeat)");
                    kvp.Value.Client?.Close();
                    connectedSensors.Remove(kvp.Key);
                }
            }
        }
    }
}
