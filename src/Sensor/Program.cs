using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using One_Health.Common.Protocol;
using One_Health.Common.Utilities;

namespace One_Health.Sensor;

/// <summary>
/// SENSOR (TP2): publica medições no broker RabbitMQ (Pub/Sub).
/// Não requer ligação TCP direta ao Gateway.
/// </summary>
internal class SensorProgram
{
    private static string   _sensorId       = string.Empty;
    private static string   _rabbitHost     = "localhost";
    private static string   _configFilePath = "config/sensors.csv";
    private static string   _zone           = "ZONA_CENTRO";
    private static string   _sensorStatus   = "ativo";
    private static string[] _supportedDataTypes = SensorDefaults.SupportedDataTypes;

    private static IConnection? _connection;
    private static IChannel?    _channel;
    private const  string       ExchangeName = "one_health";

    private static bool _isConnected = false;
    private static CancellationTokenSource _cts = new();
    private static readonly SemaphoreSlim _publishLock = new SemaphoreSlim(1, 1);

    static async Task Main(string[] args)
    {
        Console.WriteLine("=== ONE HEALTH SENSOR (TP2) ===");
        Console.WriteLine("Comunicação via RabbitMQ Pub/Sub");
        Console.WriteLine();

        if (args.Length < 1)
        {
            Console.WriteLine("Uso: dotnet run -- <sensor_id> [rabbit_host] [config_file]");
            Console.WriteLine("Exemplo: dotnet run -- S101 localhost config/sensors.csv");
            return;
        }

        _sensorId       = args[0];
        _rabbitHost     = args.Length > 1 ? args[1] : "localhost";
        _configFilePath = args.Length > 2 ? args[2] : "config/sensors.csv";

        LoadSensorConfiguration();

        Console.WriteLine($"Sensor ID:         {_sensorId}");
        Console.WriteLine($"Estado:            {_sensorStatus}");
        Console.WriteLine($"RabbitMQ:          {_rabbitHost}");
        Console.WriteLine($"Zona:              {_zone}");
        Console.WriteLine($"Tipos suportados:  {string.Join(", ", _supportedDataTypes)}");
        Console.WriteLine();

        if (_sensorStatus != "ativo")
        {
            string msg = _sensorStatus == "manutencao"
                ? $"Sensor {_sensorId} em MANUTENÇÃO — envio de dados suspenso temporariamente."
                : $"Sensor {_sensorId} DESATIVADO — não é possível enviar dados.";
            Console.WriteLine(msg);
            Console.WriteLine("\nPressione ENTER para terminar.");
            Console.ReadLine();
            return;
        }

        try
        {
            await ConnectToRabbitMQAsync();

            if (!_isConnected)
            {
                Console.WriteLine("Falha ao conectar ao RabbitMQ.");
                return;
            }

            int intervalMs = args.Length > 3 ? int.Parse(args[3]) : 60000;
            Console.WriteLine($"Auto-envio em paralelo a cada {intervalMs / 1000}s ({_supportedDataTypes.Length} threads, 1 por tipo de dado)");
            Console.WriteLine();

            // Uma thread por tipo de dado + heartbeat — todas concorrem no _publishLock
            Task heartbeatTask = Task.Run(() => SendHeartbeatPeriodicallyAsync(_cts.Token));
            Task[] dataTasks   = _supportedDataTypes
                .Where(t => t != "VIDEO")
                .Select(dataType => Task.Run(() => SendDataTypeLoopAsync(dataType, _cts.Token, intervalMs)))
                .ToArray();

            await RunMenuAsync();

            _cts.Cancel();
            await Task.WhenAll([heartbeatTask, ..dataTasks]);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro: {ex.Message}");
        }
        finally
        {
            _channel?.Dispose();
            _connection?.Dispose();
            Console.WriteLine("Sensor desconectado.");
        }
    }

    // ── RabbitMQ ─────────────────────────────────────────────────────────────

    static async Task ConnectToRabbitMQAsync()
    {
        try
        {
            Console.WriteLine($"A conectar ao RabbitMQ em {_rabbitHost}...");
            var factory = new ConnectionFactory { HostName = _rabbitHost };
            _connection = await factory.CreateConnectionAsync();
            _channel    = await _connection.CreateChannelAsync();

            await _channel.ExchangeDeclareAsync(ExchangeName, ExchangeType.Topic, durable: true);

            _isConnected = true;
            Console.WriteLine("Conectado ao RabbitMQ!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao conectar ao RabbitMQ: {ex.Message}");
            _isConnected = false;
        }
    }

    static async Task PublishAsync<T>(T message, string routingKey) where T : Message
    {
        if (_channel == null)
            throw new InvalidOperationException("Canal RabbitMQ não disponível");

        await _publishLock.WaitAsync();
        try
        {
            string json = MessageSerializer.Serialize(message);
            byte[] body = Encoding.UTF8.GetBytes(json);
            var props   = new BasicProperties { Persistent = true };
            await _channel.BasicPublishAsync(ExchangeName, routingKey, false, props, body);
        }
        finally
        {
            _publishLock.Release();
        }
    }

    // ── Menu ─────────────────────────────────────────────────────────────────

    static async Task RunMenuAsync()
    {
        while (_isConnected)
        {
            Console.WriteLine("\n--- MENU ---");
            Console.WriteLine("1. Enviar medição manual (DATA)");
            Console.WriteLine("2. Enviar heartbeat");
            Console.WriteLine("0. Sair");
            Console.WriteLine("[Auto-envio a correr em background]");
            Console.Write("Opção: ");

            string? option = Console.ReadLine();
            switch (option)
            {
                case "1": await SendDataInteractiveAsync(); break;
                case "2": await SendHeartbeatAsync();       break;
                case "0": _isConnected = false;             break;
                default:  Console.WriteLine("Opção inválida."); break;
            }
        }
    }

    static async Task SendDataInteractiveAsync()
    {
        Console.WriteLine("\nTipos de dados disponíveis:");
        for (int i = 0; i < _supportedDataTypes.Length; i++)
            Console.WriteLine($"  {i + 1}. {_supportedDataTypes[i]}");

        Console.Write("Selecione tipo (número): ");
        if (!int.TryParse(Console.ReadLine(), out int idx) || idx < 1 || idx > _supportedDataTypes.Length)
        {
            Console.WriteLine("Tipo inválido.");
            return;
        }

        string dataType = _supportedDataTypes[idx - 1];

        Console.Write("Valor (ENTER = aleatório): ");
        string? valueInput = Console.ReadLine();

        double value;
        if (string.IsNullOrWhiteSpace(valueInput))
        {
            value = SensorDefaults.GetRandomValue(dataType);
            Console.WriteLine($"Valor gerado: {value}");
        }
        else if (!double.TryParse(valueInput, System.Globalization.NumberStyles.Any,
                                  System.Globalization.CultureInfo.InvariantCulture, out value))
        {
            Console.WriteLine("Valor inválido.");
            return;
        }

        var dataMsg = new DataMessage { SensorId = _sensorId, DataType = dataType, Value = value };

        // Routing key: sensor.<ZONE>.<DATA_TYPE>
        string routingKey = $"sensor.{_zone}.{dataType}";
        await PublishAsync(dataMsg, routingKey);

        Console.WriteLine($"Publicado: {routingKey} = {value}");
    }

    static async Task SendHeartbeatAsync()
    {
        var hbMsg = new HeartbeatMessage { SensorId = _sensorId };
        await PublishAsync(hbMsg, $"heartbeat.{_sensorId}");
        Console.WriteLine("Heartbeat publicado.");
    }

    static async Task SendHeartbeatPeriodicallyAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested && _isConnected)
            {
                await Task.Delay(120000, token);
                if (_isConnected)
                    await SendHeartbeatAsync();
            }
        }
        catch (TaskCanceledException) { }
        catch (Exception ex)
        {
            Console.WriteLine($"Heartbeat thread: {ex.Message}");
        }
    }

    // Uma thread dedicada por tipo de dado — todas concorrem simultaneamente no _publishLock
    static async Task SendDataTypeLoopAsync(string dataType, CancellationToken token, int intervalMs = 5000)
    {
        try
        {
            while (!token.IsCancellationRequested && _isConnected)
            {
                await Task.Delay(intervalMs, token);
                if (!_isConnected) break;

                double value      = SensorDefaults.GetRandomValue(dataType);
                var dataMsg       = new DataMessage { SensorId = _sensorId, DataType = dataType, Value = value };
                string routingKey = $"sensor.{_zone}.{dataType}";

                await PublishAsync(dataMsg, routingKey);
                Console.WriteLine($"[{dataType,-12} | Thread {Environment.CurrentManagedThreadId,2}] {routingKey} = {value:F2}");
            }
        }
        catch (TaskCanceledException) { }
        catch (Exception ex) { Console.WriteLine($"[{dataType}] Erro: {ex.Message}"); }
    }

    // ── Config ────────────────────────────────────────────────────────────────

    static void LoadSensorConfiguration()
    {
        try
        {
            if (!File.Exists(_configFilePath))
            {
                Console.WriteLine($"Ficheiro de configuração não encontrado: {_configFilePath}");
                return;
            }

            foreach (var line in File.ReadLines(_configFilePath))
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#") ||
                    line.StartsWith("sensor_id", StringComparison.OrdinalIgnoreCase))
                    continue;

                var parts = line.Split(':', 5);
                if (parts.Length < 5) continue;
                if (!string.Equals(parts[0].Trim(), _sensorId, StringComparison.OrdinalIgnoreCase))
                    continue;

                _sensorStatus = parts[1].Trim();
                _zone = parts[2].Trim();

                string typesPart = parts[3].Trim().TrimStart('[').TrimEnd(']');
                var types = typesPart.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                                     .Where(t => !string.IsNullOrWhiteSpace(t))
                                     .ToArray();
                if (types.Length > 0) _supportedDataTypes = types;
                return;
            }

            Console.WriteLine($"Sensor {_sensorId} não encontrado em {_configFilePath}. A usar defaults.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro a ler configuração: {ex.Message}");
        }
    }
}
