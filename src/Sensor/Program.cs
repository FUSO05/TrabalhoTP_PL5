using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using One_Health.Common.Protocol;
using One_Health.Common.Utilities;

namespace One_Health.Sensor;

/// <summary>
/// Implementação do SENSOR na Fase 2
/// Conecta ao GATEWAY, envia medições, e recebe confirmações
/// </summary>
internal class SensorProgram
{
    private static string _sensorId = string.Empty;
    private static string _gatewayIp = string.Empty;
    private static int _gatewayPort = 9000;
    private static string _configFilePath = "config/sensors.csv";
    private static TcpClient? _client;
    private static NetworkStream? _stream;
    private static string? _token;
    private static string _zone = "ZONA_CENTRO";
    private static string[] _supportedDataTypes = SensorDefaults.SupportedDataTypes;
    private static bool _isConnected = false;
    private static bool _isRegistered = false;
    private static bool _isStreaming = false;
    private static bool _isLowBattery = false;
    private static CancellationTokenSource _cancellationTokenSource = new();
    private static Random _random = new();

    static void Main(string[] args)
    {
        Console.WriteLine("=== ONE HEALTH SENSOR ===");
        Console.WriteLine("Fase 2 - Implementação de SENSOR simples");
        Console.WriteLine();

        if (args.Length < 2)
        {
            Console.WriteLine("Uso: dotnet run -- <sensor_id> <gateway_ip> [gateway_port] [config_file]");
            Console.WriteLine("Exemplo: dotnet run -- S101 127.0.0.1 9000 config/sensors.csv");
            return;
        }

        _sensorId = args[0];
        _gatewayIp = args[1];
        _gatewayPort = args.Length > 2 ? int.Parse(args[2]) : 9000;
        _configFilePath = args.Length > 3 ? args[3] : "config/sensors.csv";

        LoadSensorConfiguration();

        Console.WriteLine($"Sensor ID: {_sensorId}");
        Console.WriteLine($"Gateway: {_gatewayIp}:{_gatewayPort}");
        Console.WriteLine($"Config: {_configFilePath}");
        Console.WriteLine($"Zona: {_zone}");
        Console.WriteLine($"Tipos suportados: {string.Join(",", _supportedDataTypes)}");
        Console.WriteLine();

        try
        {
            ConnectToGateway();

            if (!_isConnected)
            {
                Console.WriteLine("Falha ao conectar ao Gateway.");
                return;
            }

            RegisterWithGateway();

            if (!_isRegistered)
            {
                Console.WriteLine("Falha ao registar no Gateway.");
                return;
            }

            // Inicia thread de heartbeat automático
            Task heartbeatTask = Task.Run(() => SendHeartbeatPeriodically(_cancellationTokenSource.Token));

            // Menu interativo
            RunMenu();

            // Encerra heartbeat
            _cancellationTokenSource.Cancel();
            Task.WaitAll(heartbeatTask);

            // Desconecta
            SendDisconnectMessage();
            _stream?.Close();
            _client?.Close();

            Console.WriteLine("Sensor desconectado.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro: {ex.Message}");
        }
    }

    static void ConnectToGateway()
    {
        try
        {
            Console.WriteLine($"Conectando ao Gateway em {_gatewayIp}:{_gatewayPort}...");
            _client = new TcpClient();
            _client.Connect(_gatewayIp, _gatewayPort);
            _stream = _client.GetStream();
            _isConnected = true;
            Console.WriteLine("Conectado ao Gateway!");

            // Envia CONNECT
            var connectMsg = new ConnectMessage { SensorId = _sensorId };
            SendMessage(connectMsg);

            // Recebe resposta
            ResponseMessage? response = ReceiveMessage<ResponseMessage>();
            if (response?.Status == "OK")
            {
                _token = response.Message;
                Console.WriteLine($"Token recebido: {_token}");
            }
            else
            {
                _isConnected = false;
                Console.WriteLine($"Gateway rejeitou conexão: {response?.Message}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro na conexão: {ex.Message}");
            _isConnected = false;
        }
    }

    static void RegisterWithGateway()
    {
        try
        {
            Console.WriteLine("Registando com o Gateway...");
            var registerMsg = new RegisterMessage
            {
                SensorId = _sensorId,
                Token = _token ?? string.Empty,
                Zone = _zone,
                SupportedDataTypes = _supportedDataTypes.ToList()
            };
            SendMessage(registerMsg);

            // Recebe resposta
            ResponseMessage? response = ReceiveMessage<ResponseMessage>();
            if (response?.Status == "OK")
            {
                _isRegistered = true;
                Console.WriteLine("Registado com sucesso!");
            }
            else
            {
                Console.WriteLine($"Gateway rejeitou registo: {response?.Message}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro no registo: {ex.Message}");
        }
    }

    static void RunMenu()
    {
        bool running = true;
        while (running && _isConnected && _isRegistered)
        {
            Console.WriteLine("\n--- MENU ---");
            Console.WriteLine("1. Enviar medição (DATA)");
            Console.WriteLine("2. Enviar heartbeat (HEARTBEAT)");
            Console.WriteLine("3. Requisitar stream (STREAM_REQUEST)");
            Console.WriteLine("5. Alternar modo bateria baixa");
            Console.WriteLine("4. Desconectar (DISCONNECT)");
            Console.WriteLine("0. Sair");
            Console.Write("Opção: ");

            string? option = Console.ReadLine();

            switch (option)
            {
                case "1":
                    SendDataInteractive();
                    break;
                case "2":
                    SendHeartbeatMessage();
                    break;
                case "3":
                    RequestStreamInteractive();
                    break;
                case "5":
                    ToggleLowBatteryMode();
                    break;
                case "4":
                    running = false;
                    break;
                case "0":
                    running = false;
                    break;
                default:
                    Console.WriteLine("Opção inválida.");
                    break;
            }
        }
    }

    static void SendDataInteractive()
    {
        var availableDataTypes = _supportedDataTypes;

        Console.WriteLine("\nTipos de dados disponíveis:");
        for (int i = 0; i < availableDataTypes.Length; i++)
        {
            Console.WriteLine($"{i + 1}. {availableDataTypes[i]}");
        }
        Console.Write("Selecione tipo de dado (número): ");

        if (!int.TryParse(Console.ReadLine(), out int typeIndex) || typeIndex < 1 || typeIndex > availableDataTypes.Length)
        {
            Console.WriteLine("Tipo inválido.");
            return;
        }

        string dataType = availableDataTypes[typeIndex - 1];

        Console.Write("Valor (enter para gerar aleatório): ");
        string? valueInput = Console.ReadLine();

        double value;
        if (string.IsNullOrWhiteSpace(valueInput))
        {
            value = SensorDefaults.GetRandomValue(dataType);
            Console.WriteLine($"Valor gerado aleatoriamente: {value}");
        }
        else
        {
            if (!double.TryParse(valueInput, out value))
            {
                Console.WriteLine("Valor inválido.");
                return;
            }
        }

        var dataMsg = new DataMessage
        {
            SensorId = _sensorId,
            DataType = dataType,
            Value = value
        };
        SendMessage(dataMsg);

        // Recebe resposta do Gateway
        ResponseMessage? response = ReceiveMessage<ResponseMessage>();
        if (response?.Status == "OK")
        {
            Console.WriteLine($"Medição enviada com sucesso!");
        }
        else
        {
            Console.WriteLine($"Erro ao enviar medição: {response?.Message}");
        }
    }

    static void LoadSensorConfiguration()
    {
        try
        {
            if (!File.Exists(_configFilePath))
            {
                Console.WriteLine($"⚠️  Ficheiro de configuração não encontrado: {_configFilePath}");
                return;
            }

            foreach (var line in File.ReadLines(_configFilePath))
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                    continue;

                if (line.StartsWith("sensor_id", StringComparison.OrdinalIgnoreCase))
                    continue;

                var parts = line.Split(':', 5);
                if (parts.Length < 5)
                    continue;

                if (!string.Equals(parts[0].Trim(), _sensorId, StringComparison.OrdinalIgnoreCase))
                    continue;

                _zone = parts[2].Trim();

                string typesPart = parts[3].Trim().TrimStart('[').TrimEnd(']');
                var types = typesPart
                    .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .ToArray();

                if (types.Length > 0)
                {
                    _supportedDataTypes = types;
                }

                return;
            }

            Console.WriteLine($"⚠️  Sensor {_sensorId} não encontrado em {_configFilePath}. Usando defaults.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Erro a ler configuração: {ex.Message}");
        }
    }

    static void SendHeartbeatMessage()
    {
        var heartbeatMsg = new HeartbeatMessage
        {
            SensorId = _sensorId,
            IntervalSeconds = GetHeartbeatIntervalSeconds(),
            IsStreaming = _isStreaming,
            IsLowBattery = _isLowBattery
        };
        SendMessage(heartbeatMsg);
        Console.WriteLine($"Heartbeat enviado (intervalo={heartbeatMsg.IntervalSeconds}s, streaming={heartbeatMsg.IsStreaming}, bateria_baixa={heartbeatMsg.IsLowBattery}).");
    }

    static void RequestStreamInteractive()
    {
        Console.Write("Endpoint do stream (ex: 192.168.1.100:5000): ");
        string? endpoint = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            Console.WriteLine("Endpoint inválido.");
            return;
        }

        var streamMsg = new StreamRequestMessage
        {
            SensorId = _sensorId,
            StreamType = "VIDEO",
            StreamEndpoint = endpoint
        };
        SendMessage(streamMsg);

        // Recebe resposta
        ResponseMessage? response = ReceiveMessage<ResponseMessage>();
        Console.WriteLine($"Resposta: {response?.Message}");

        if (response?.Status == "OK")
        {
            _isStreaming = true;
            SimulateVideoStream();
            _isStreaming = false;
        }
    }

    static void SimulateVideoStream()
    {
        string streamId = $"{_sensorId}_{DateTime.Now:yyyyMMddHHmmss}";

        var startMsg = new StreamStartMessage
        {
            SensorId = _sensorId,
            StreamId = streamId,
            StreamType = "VIDEO"
        };

        SendMessage(startMsg);
        ResponseMessage? startResponse = ReceiveMessage<ResponseMessage>();
        if (startResponse?.Status != "OK")
        {
            Console.WriteLine($"Falha ao iniciar stream: {startResponse?.Message}");
            return;
        }

        Console.WriteLine($"Stream iniciada: {streamId}");

        const int frameCount = 5;
        for (int i = 1; i <= frameCount; i++)
        {
            byte[] fakeFrame = RandomNumberGenerator.GetBytes(256);
            string payload = Convert.ToBase64String(fakeFrame);

            var frameMsg = new StreamFrameMessage
            {
                SensorId = _sensorId,
                StreamId = streamId,
                Sequence = i,
                PayloadBase64 = payload
            };

            SendMessage(frameMsg);
            ResponseMessage? frameResponse = ReceiveMessage<ResponseMessage>();
            if (frameResponse?.Status != "OK")
            {
                Console.WriteLine($"Falha no frame {i}: {frameResponse?.Message}");
                break;
            }

            Console.WriteLine($"Frame {i}/{frameCount} enviado");
            Thread.Sleep(200);
        }

        var endMsg = new StreamEndMessage
        {
            SensorId = _sensorId,
            StreamId = streamId,
            Reason = "Fim da simulação"
        };

        SendMessage(endMsg);
        ResponseMessage? endResponse = ReceiveMessage<ResponseMessage>();
        Console.WriteLine($"Fim da stream: {endResponse?.Message}");
    }

    static void SendDisconnectMessage()
    {
        try
        {
            var disconnectMsg = new DisconnectMessage { SensorId = _sensorId };
            SendMessage(disconnectMsg);

            ResponseMessage? response = ReceiveMessage<ResponseMessage>();
            if (response?.Status == "OK")
            {
                Console.WriteLine("Desconexão confirmada pelo Gateway.");
            }
            else
            {
                Console.WriteLine($"Falha ao desconectar: {response?.Message}");
            }

            _isRegistered = false;
            _isConnected = false;
        }
        catch
        {
            // Ignora erros ao desconectar
        }
    }

    static void SendHeartbeatPeriodically(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && _isConnected)
            {
                int interval = GetHeartbeatIntervalSeconds();

                if (cancellationToken.WaitHandle.WaitOne(TimeSpan.FromSeconds(interval)))
                {
                    break;
                }

                if (_isConnected && _isRegistered)
                {
                    SendHeartbeatMessage();
                }
            }
        }
        catch
        {
            // Ignora exceções na thread de heartbeat
        }
    }

    static int GetHeartbeatIntervalSeconds()
    {
        if (_isLowBattery)
        {
            return 300;
        }

        if (_isStreaming)
        {
            return 20;
        }

        return 120;
    }

    static void ToggleLowBatteryMode()
    {
        _isLowBattery = !_isLowBattery;
        Console.WriteLine(_isLowBattery
            ? "Modo bateria baixa ATIVADO (heartbeat ~300s)."
            : "Modo bateria baixa DESATIVADO.");
    }

    static void SendMessage<T>(T message) where T : Message
    {
        if (_stream == null)
            throw new InvalidOperationException("Não ligado ao Gateway");

        string json = MessageSerializer.Serialize(message);
        byte[] data = Encoding.UTF8.GetBytes(json + "\n");
        _stream.Write(data, 0, data.Length);
        _stream.Flush();
    }

    static T? ReceiveMessage<T>() where T : Message
    {
        if (_stream == null)
            throw new InvalidOperationException("Não ligado ao Gateway");

        using (var reader = new StreamReader(_stream, Encoding.UTF8, false, 1024, true))
        {
            string? line = reader.ReadLine();
            if (line == null)
                return null;

            // For ResponseMessage, use MessageFactory
            if (typeof(T) == typeof(ResponseMessage))
            {
                var msg = MessageFactory.DeserializeMessage(line);
                return msg as T;
            }

            return MessageSerializer.Deserialize<T>(line);
        }
    }
}
