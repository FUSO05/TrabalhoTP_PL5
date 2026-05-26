using System.Net;
using System.Net.Sockets;
using System.Text;
using One_Health.Common.Protocol;
using One_Health.Common.Utilities;

namespace One_Health.Server.Services;

/// <summary>
/// Background service: listens for STORE messages from the Gateway on TCP port 8000.
/// </summary>
public class GatewayListenerService : BackgroundService
{
    private readonly ILogger<GatewayListenerService> _logger;
    private readonly DatabaseService _db;
    private readonly IConfiguration _config;
    private TcpListener? _listener;
    private static readonly Mutex _fileMutex = new();

    public GatewayListenerService(ILogger<GatewayListenerService> logger, DatabaseService db, IConfiguration config)
    {
        _logger = logger;
        _db     = db;
        _config = config;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        int port = _config.GetValue<int>("TcpPort", 8000);
        _listener = new TcpListener(IPAddress.Any, port);
        _listener.Start();
        _logger.LogInformation("Gateway TCP listener iniciado na porta {Port}", port);

        Console.WriteLine($"TCP listener (Gateway) na porta {port}");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                TcpClient client = await _listener.AcceptTcpClientAsync(stoppingToken);
                _logger.LogInformation("Gateway conectado: {EP}", client.Client.RemoteEndPoint);
                _ = Task.Run(() => HandleClientAsync(client, stoppingToken), stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _logger.LogError(ex, "Erro ao aceitar conexão"); }
        }

        _listener.Stop();
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        using (client)
        try
        {
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.UTF8);
            using var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };

            string? line;
            while (!ct.IsCancellationRequested && (line = await reader.ReadLineAsync(ct)) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                ProcessMessage(line, writer);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug("Gateway desconectado: {Msg}", ex.Message);
        }
    }

    private void ProcessMessage(string json, StreamWriter writer)
    {
        try
        {
            var msg = MessageFactory.DeserializeMessage(json);
            if (msg == null)
            {
                Send(writer, StorageResponseMessage.Error("Mensagem inválida"));
                return;
            }

            if (msg.MessageType == "STORE")
                HandleStore(json, writer);
            else
                Send(writer, StorageResponseMessage.Error($"Tipo não suportado: {msg.MessageType}"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar mensagem");
            Send(writer, StorageResponseMessage.Error(ex.Message));
        }
    }

    private void HandleStore(string json, StreamWriter writer)
    {
        var msg = MessageSerializer.Deserialize<StoreMessage>(json);
        if (msg == null) { Send(writer, StorageResponseMessage.Error("STORE inválido")); return; }

        if (!ValidateDataValue(msg.DataType, msg.Value))
        {
            Send(writer, StorageResponseMessage.Error($"Valor fora dos limites para {msg.DataType}"));
            return;
        }

        _db.StoreMeasurement(msg.SensorId, msg.Zone, msg.DataType, msg.Value, msg.Timestamp);
        Console.WriteLine($"  [DB] STORE: {msg.SensorId} {msg.DataType}={msg.Value} zona={msg.Zone}");
        Send(writer, StorageResponseMessage.Stored("Dados armazenados com sucesso"));
    }

    private static bool ValidateDataValue(string dataType, double value) => dataType switch
    {
        "TEMP"         => value >= -50   && value <= 60,
        "HUM"          => value >= 0     && value <= 100,
        "PM2.5"        => value >= 0     && value <= 1000,
        "PM10"         => value >= 0     && value <= 1000,
        "RUIDO"        => value >= 0     && value <= 150,
        "AR"           => value >= 0     && value <= 500,
        "LUMINOSIDADE" => value >= 0     && value <= 150000,
        _              => true
    };

    private static void Send(StreamWriter writer, StorageResponseMessage msg)
        => writer.WriteLine(MessageSerializer.Serialize(msg));
}
