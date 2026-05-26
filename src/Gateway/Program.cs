using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using Grpc.Net.Client;
using PreProcessing;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using One_Health.Common.Protocol;
using One_Health.Common.Utilities;

namespace One_Health.Gateway;

/// <summary>
/// GATEWAY (TP2): subscreve dados dos SENSORES via RabbitMQ (Pub/Sub),
/// invoca o Serviço de Pré-processamento via gRPC,
/// e encaminha dados validados ao SERVIDOR via TCP.
/// </summary>
internal class GatewayProgram
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

    // TCP → Server
    private static TcpClient? _serverConnection;
    private static string _serverIp   = "127.0.0.1";
    private static int    _serverPort = 8000;
    private static readonly Mutex _serverWriteMutex = new();

    // Config
    private static SensorConfigurationManager? _configManager;
    private static string _configFile = "config/sensors.csv";

    // RabbitMQ
    private static string _rabbitHost = "localhost";
    private static string _gatewayId  = "GW1";
    private static IConnection? _rabbitConnection;
    private static IChannel? _rabbitChannel;
    private const string ExchangeName = "one_health";

    // gRPC – PreProcessor
    private static GrpcChannel? _grpcChannel;
    private static PreProcessingService.PreProcessingServiceClient? _preProcessor;
    private static string _preprocessorAddress = "http://localhost:50051";

    static async Task Main(string[] args)
    {
        Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║   ONE HEALTH GATEWAY (TP2)                                 ║");
        Console.WriteLine("║   RabbitMQ Subscriber + gRPC Pre-Processamento             ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        _configFile          = args.Length > 0 ? args[0] : "config/sensors.csv";
        _serverIp            = args.Length > 1 ? args[1] : "127.0.0.1";
        _serverPort          = args.Length > 2 ? int.Parse(args[2]) : 8000;
        _rabbitHost          = args.Length > 3 ? args[3] : "localhost";
        _preprocessorAddress = args.Length > 4 ? args[4] : "http://localhost:50051";
        _gatewayId           = args.Length > 5 ? args[5] : "GW1";

        Console.WriteLine($"Gateway ID:      {_gatewayId}");
        Console.WriteLine($"Config:          {_configFile}");
        Console.WriteLine($"Servidor:        {_serverIp}:{_serverPort}");
        Console.WriteLine($"RabbitMQ:        {_rabbitHost}");
        Console.WriteLine($"Pre-Processador: {_preprocessorAddress}");
        Console.WriteLine();

        _configManager = new SensorConfigurationManager(_configFile);

        // Connect to gRPC PreProcessor
        _grpcChannel  = GrpcChannel.ForAddress(_preprocessorAddress);
        _preProcessor = new PreProcessingService.PreProcessingServiceClient(_grpcChannel);
        Console.WriteLine($"Conectado ao Pre-Processador gRPC em {_preprocessorAddress}");

        // Connect to TCP Server
        if (!ConnectToServer())
        {
            Console.WriteLine("Servidor nao disponivel. Verifique se o servidor esta a correr.");
            return;
        }

        // Connect to RabbitMQ and subscribe
        await StartRabbitMQSubscriberAsync();

        Console.WriteLine("\nGateway ativo. Pressione ENTER para terminar.");
        Console.ReadLine();

        _rabbitChannel?.Dispose();
        _rabbitConnection?.Dispose();
        _grpcChannel?.Dispose();
        _serverConnection?.Close();
    }

    // ── TCP Server ───────────────────────────────────────────────────────────

    private static bool ConnectToServer()
    {
        try
        {
            _serverConnection = new TcpClient();
            _serverConnection.Connect(_serverIp, _serverPort);
            Console.WriteLine($"Conectado ao servidor {_serverIp}:{_serverPort}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao conectar ao servidor: {ex.Message}");
            return false;
        }
    }

    private static bool ForwardToServer(string sensorId, string zone, string dataType, double value, DateTime timestamp)
    {
        try
        {
            if (_serverConnection == null || !_serverConnection.Connected)
            {
                Console.WriteLine("  Conexao com servidor perdida. A reconectar...");
                if (!ConnectToServer()) return false;
            }

            var storeMsg = new StoreMessage
            {
                SensorId  = sensorId,
                Zone      = zone,
                DataType  = dataType,
                Value     = value,
                Timestamp = timestamp
            };

            string json = MessageSerializer.Serialize(storeMsg);

            _serverWriteMutex.WaitOne();
            try
            {
                NetworkStream stream = _serverConnection!.GetStream();

                using StreamWriter writer = new(stream, Utf8NoBom, 1024, leaveOpen: true) { AutoFlush = true };
                writer.WriteLine(json);

                using StreamReader reader = new(stream, Utf8NoBom, false, 1024, leaveOpen: true);
                string? responseLine = reader.ReadLine();

                if (string.IsNullOrWhiteSpace(responseLine))
                {
                    Console.WriteLine("  Sem resposta do servidor");
                    return false;
                }

                responseLine = responseLine.TrimStart('﻿');
                var storageResponse = MessageFactory.DeserializeMessage(responseLine) as StorageResponseMessage
                                      ?? MessageSerializer.Deserialize<StorageResponseMessage>(responseLine);

                if (storageResponse?.Status != "STORED")
                {
                    Console.WriteLine($"  Servidor rejeitou: {storageResponse?.Message}");
                    return false;
                }
            }
            finally
            {
                _serverWriteMutex.ReleaseMutex();
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Erro ao encaminhar para servidor: {ex.Message}");
            return false;
        }
    }

    // ── RabbitMQ Pub/Sub ─────────────────────────────────────────────────────

    private static async Task StartRabbitMQSubscriberAsync()
    {
        var factory = new ConnectionFactory { HostName = _rabbitHost };

        _rabbitConnection = await factory.CreateConnectionAsync();
        _rabbitChannel    = await _rabbitConnection.CreateChannelAsync();

        // Declare topic exchange (must match what sensors declare)
        await _rabbitChannel.ExchangeDeclareAsync(ExchangeName, ExchangeType.Topic, durable: true);

        // Data queue: binds to sensor.# (all zones, all types)
        // Fila única por Gateway — permite múltiplos Gateways a receber todas as mensagens
        var dataQueue = await _rabbitChannel.QueueDeclareAsync(queue: $"gateway_data_{_gatewayId}", durable: true, exclusive: false, autoDelete: false);
        await _rabbitChannel.QueueBindAsync(dataQueue.QueueName, ExchangeName, "sensor.#");

        // Heartbeat queue
        var hbQueue = await _rabbitChannel.QueueDeclareAsync(queue: $"gateway_heartbeat_{_gatewayId}", durable: true, exclusive: false, autoDelete: false);
        await _rabbitChannel.QueueBindAsync(hbQueue.QueueName, ExchangeName, "heartbeat.#");

        // Data consumer
        var dataConsumer = new AsyncEventingBasicConsumer(_rabbitChannel);
        dataConsumer.ReceivedAsync += OnDataMessageAsync;
        await _rabbitChannel.BasicConsumeAsync(dataQueue.QueueName, autoAck: false, consumer: dataConsumer);

        // Heartbeat consumer
        var hbConsumer = new AsyncEventingBasicConsumer(_rabbitChannel);
        hbConsumer.ReceivedAsync += OnHeartbeatMessageAsync;
        await _rabbitChannel.BasicConsumeAsync(hbQueue.QueueName, autoAck: false, consumer: hbConsumer);

        Console.WriteLine("Subscrito ao RabbitMQ:");
        Console.WriteLine($"  Exchange: {ExchangeName}");
        Console.WriteLine($"  Dados:    sensor.#  → fila gateway_data");
        Console.WriteLine($"  Heartbeat: heartbeat.# → fila gateway_heartbeat");
    }

    private static async Task OnDataMessageAsync(object sender, BasicDeliverEventArgs ea)
    {
        string routingKey = ea.RoutingKey; // sensor.{ZONE}.{DATA_TYPE}
        string body       = Encoding.UTF8.GetString(ea.Body.ToArray());

        try
        {
            // Parse routing key: sensor.<ZONE>.<DATA_TYPE>
            var parts    = routingKey.Split('.', 3);
            string zone  = parts.Length >= 2 ? parts[1] : "DESCONHECIDA";
            string dType = parts.Length >= 3 ? parts[2] : "UNKNOWN";

            // Deserialize payload
            var dataMsg = MessageSerializer.Deserialize<DataMessage>(body);
            if (dataMsg == null)
            {
                Console.WriteLine($"  Mensagem invalida na fila: {body}");
                await _rabbitChannel!.BasicNackAsync(ea.DeliveryTag, false, false);
                return;
            }

            string sensorId = dataMsg.SensorId;
            Console.WriteLine($"  [RabbitMQ] DATA: {sensorId} {dType}={dataMsg.Value} (zona={zone})");

            // Validate sensor in CSV
            var sensorConfig = _configManager?.GetSensor(sensorId);
            if (sensorConfig == null || sensorConfig.Status != "ativo")
            {
                Console.WriteLine($"  Sensor {sensorId} nao ativo ou nao registado. Descartado.");
                await _rabbitChannel!.BasicAckAsync(ea.DeliveryTag, false);
                return;
            }

            if (!_configManager!.IsSensorSupportingDataType(sensorId, dataMsg.DataType))
            {
                Console.WriteLine($"  Tipo {dataMsg.DataType} nao suportado por {sensorId}. Descartado.");
                await _rabbitChannel!.BasicAckAsync(ea.DeliveryTag, false);
                return;
            }

            // Call PreProcessor gRPC
            var rawData = new RawSensorData
            {
                SensorId = sensorId,
                Zone     = zone,
                DataType = dataMsg.DataType,
                Value    = dataMsg.Value,
                Timestamp = dataMsg.Timestamp.ToString("O")
            };

            ProcessedSensorData processed;
            try
            {
                processed = await _preProcessor!.PreProcessAsync(rawData);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  gRPC PreProcessor falhou: {ex.Message}. Usando valor original.");
                processed = new ProcessedSensorData
                {
                    SensorId       = sensorId,
                    Zone           = zone,
                    DataType       = dataMsg.DataType,
                    NormalizedValue = dataMsg.Value,
                    IsValid        = true,
                    Timestamp      = dataMsg.Timestamp.ToString("O")
                };
            }

            if (!processed.IsValid)
            {
                Console.WriteLine($"  Pre-processamento rejeitou dado: {processed.ErrorMessage}");
                await _rabbitChannel!.BasicAckAsync(ea.DeliveryTag, false);
                return;
            }

            Console.WriteLine($"  Pre-processado: valor={processed.NormalizedValue} {processed.Unit}");

            // Forward to server
            bool forwarded = ForwardToServer(sensorId, zone, processed.DataType,
                                             processed.NormalizedValue, dataMsg.Timestamp);
            if (forwarded)
            {
                Console.WriteLine($"  Encaminhado ao servidor com sucesso.");
                _configManager.UpdateLastSync(sensorId);
            }
            else
            {
                Console.WriteLine($"  Falha ao encaminhar ao servidor.");
            }

            await _rabbitChannel!.BasicAckAsync(ea.DeliveryTag, false);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Erro ao processar mensagem DATA: {ex.Message}");
            await _rabbitChannel!.BasicNackAsync(ea.DeliveryTag, false, false);
        }
    }

    private static async Task OnHeartbeatMessageAsync(object sender, BasicDeliverEventArgs ea)
    {
        string body = Encoding.UTF8.GetString(ea.Body.ToArray());
        try
        {
            var msg = MessageSerializer.Deserialize<HeartbeatMessage>(body);
            if (msg != null)
            {
                Console.WriteLine($"  [RabbitMQ] HEARTBEAT: {msg.SensorId}");
                _configManager?.UpdateLastSync(msg.SensorId);
            }
            await _rabbitChannel!.BasicAckAsync(ea.DeliveryTag, false);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Erro ao processar HEARTBEAT: {ex.Message}");
            await _rabbitChannel!.BasicNackAsync(ea.DeliveryTag, false, false);
        }
    }
}
