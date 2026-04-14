using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
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
    private static TcpClient? _client;
    private static NetworkStream? _stream;
    private static string? _token;
    private static string _zone = "ZONA_CENTRO";
    private static bool _isConnected = false;
    private static bool _isRegistered = false;
    private static CancellationTokenSource _cancellationTokenSource = new();
    private static Random _random = new();

    static void Main(string[] args)
    {
        Console.WriteLine("=== ONE HEALTH SENSOR ===");
        Console.WriteLine("Fase 2 - Implementação de SENSOR simples");
        Console.WriteLine();

        if (args.Length < 2)
        {
            Console.WriteLine("Uso: dotnet run -- <sensor_id> <gateway_ip> [gateway_port]");
            Console.WriteLine("Exemplo: dotnet run -- S101 127.0.0.1 9000");
            return;
        }

        _sensorId = args[0];
        _gatewayIp = args[1];
        _gatewayPort = args.Length > 2 ? int.Parse(args[2]) : 9000;

        Console.WriteLine($"Sensor ID: {_sensorId}");
        Console.WriteLine($"Gateway: {_gatewayIp}:{_gatewayPort}");
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
                SupportedDataTypes = SensorDefaults.SupportedDataTypes.ToList()
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
        Console.WriteLine("\nTipos de dados disponíveis:");
        for (int i = 0; i < SensorDefaults.SupportedDataTypes.Length; i++)
        {
            Console.WriteLine($"{i + 1}. {SensorDefaults.SupportedDataTypes[i]}");
        }
        Console.Write("Selecione tipo de dado (número): ");

        if (!int.TryParse(Console.ReadLine(), out int typeIndex) || typeIndex < 1 || typeIndex > SensorDefaults.SupportedDataTypes.Length)
        {
            Console.WriteLine("Tipo inválido.");
            return;
        }

        string dataType = SensorDefaults.SupportedDataTypes[typeIndex - 1];

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

    static void SendHeartbeatMessage()
    {
        var heartbeatMsg = new HeartbeatMessage { SensorId = _sensorId };
        SendMessage(heartbeatMsg);
        Console.WriteLine("Heartbeat enviado.");
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
    }

    static void SendDisconnectMessage()
    {
        try
        {
            var disconnectMsg = new DisconnectMessage { SensorId = _sensorId };
            SendMessage(disconnectMsg);
            Console.WriteLine("Mensagem de desconexão enviada.");
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
                Thread.Sleep(30000); // 30 segundos
                if (_isConnected)
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

            return MessageSerializer.Deserialize<T>(line);
        }
    }
}
