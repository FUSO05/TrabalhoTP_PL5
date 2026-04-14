using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using One_Health.Common.Protocol;
using One_Health.Common.Utilities;

namespace One_Health.Server;

/// <summary>
/// SERVIDOR: Recebe dados validados do GATEWAY e armazena em ficheiros
/// </summary>
internal class ServerProgram
{
    private static TcpListener? listener;
    private static string dataDirectory = "./data";
    private static readonly object fileLock = new();

    static void Main(string[] args)
    {
        Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║   ONE HEALTH SERVER                                        ║");
        Console.WriteLine("║   Armazenamento de Dados                                   ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        int port = args.Length > 0 ? int.Parse(args[0]) : 8000;
        dataDirectory = args.Length > 1 ? args[1] : "./data";

        Console.WriteLine($"📡 Porta de escuta: {port}");
        Console.WriteLine($"📁 Diretório de dados: {dataDirectory}");
        Console.WriteLine();

        // Criar diretório se não existir
        if (!Directory.Exists(dataDirectory))
        {
            Directory.CreateDirectory(dataDirectory);
            Console.WriteLine($"✓ Diretório criado: {dataDirectory}");
        }

        try
        {
            listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            Console.WriteLine($"✓ Servidor iniciado na porta {port}");
            Console.WriteLine("⏳ Aguardando conexões do GATEWAY...\n");

            // Loop de aceitação de conexões
            while (true)
            {
                TcpClient client = listener.AcceptTcpClient();
                Console.WriteLine($"→ Conexão recebida de {client.Client.RemoteEndPoint}");

                // Processar cliente em thread separada
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
        }
    }

    private static void HandleClient(TcpClient client)
    {
        try
        {
            using (NetworkStream stream = client.GetStream())
            using (StreamReader reader = new(stream, Encoding.UTF8))
            using (StreamWriter writer = new(stream, Encoding.UTF8) { AutoFlush = true })
            {
                string? line;
                while ((line = reader.ReadLine()) != null && line.Length > 0)
                {
                    Console.WriteLine($"  📨 Mensagem recebida: {line.Substring(0, Math.Min(100, line.Length))}...");

                    ProcessMessage(line, writer);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ Erro ao processar cliente: {ex.Message}");
        }
        finally
        {
            client.Close();
            Console.WriteLine($"← Conexão fechada\n");
        }
    }

    private static void ProcessMessage(string json, StreamWriter writer)
    {
        try
        {
            // Deserializar a mensagem
            var baseMessage = MessageSerializer.Deserialize<Message>(json);

            if (baseMessage == null)
            {
                SendResponse(writer, StorageResponseMessage.Error("Mensagem inválida"));
                return;
            }

            // Processar diferentes tipos de mensagem
            switch (baseMessage.MessageType)
            {
                case "STORE":
                    HandleStoreMessage(json, writer);
                    break;

                default:
                    Console.WriteLine($"  ⚠️  Tipo de mensagem não suportado: {baseMessage.MessageType}");
                    SendResponse(writer, StorageResponseMessage.Error($"Tipo de mensagem não suportado: {baseMessage.MessageType}"));
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ Erro ao processar mensagem: {ex.Message}");
            SendResponse(writer, StorageResponseMessage.Error($"Erro ao processar mensagem: {ex.Message}"));
        }
    }

    private static void HandleStoreMessage(string json, StreamWriter writer)
    {
        var storeMessage = MessageSerializer.Deserialize<StoreMessage>(json);

        if (storeMessage == null)
        {
            SendResponse(writer, StorageResponseMessage.Error("Mensagem STORE inválida"));
            return;
        }

        Console.WriteLine($"  📦 STORE: Sensor={storeMessage.SensorId}, Tipo={storeMessage.DataType}, Valor={storeMessage.Value}");

        // Validação de dados
        if (string.IsNullOrEmpty(storeMessage.SensorId))
        {
            SendResponse(writer, StorageResponseMessage.Error("SensorId não pode estar vazio"));
            return;
        }

        if (string.IsNullOrEmpty(storeMessage.DataType))
        {
            SendResponse(writer, StorageResponseMessage.Error("DataType não pode estar vazio"));
            return;
        }

        // Validar limites de valores (apenas para tipos conhecidos)
        if (!ValidateDataValue(storeMessage.DataType, storeMessage.Value))
        {
            SendResponse(writer, StorageResponseMessage.Error($"Valor fora dos limites para tipo {storeMessage.DataType}"));
            return;
        }

        // Armazenar em ficheiro
        if (StoreDataToFile(storeMessage))
        {
            Console.WriteLine($"  ✓ Dados armazenados com sucesso");
            SendResponse(writer, StorageResponseMessage.Stored("Dados armazenados com sucesso"));
        }
        else
        {
            SendResponse(writer, StorageResponseMessage.Error("Erro ao escrever ficheiro de dados"));
        }
    }

    private static bool ValidateDataValue(string dataType, double value)
    {
        // Validação simples de limites para tipos conhecidos
        return dataType switch
        {
            "TEMP" => value >= -50 && value <= 60,           // Temperatura: -50°C a 60°C
            "HUM" => value >= 0 && value <= 100,             // Humidade: 0% a 100%
            "PM2.5" => value >= 0 && value <= 1000,          // Partículas: 0 a 1000 µg/m³
            "PM10" => value >= 0 && value <= 1000,           // Partículas: 0 a 1000 µg/m³
            "RUIDO" => value >= 0 && value <= 150,           // Ruído: 0 a 150 dB
            "AR" => value >= 0 && value <= 500,              // Qualidade do ar: índice 0-500
            "LUMINOSIDADE" => value >= 0 && value <= 150000, // Luminosidade: lux
            _ => true // Outros tipos não têm validação específica
        };
    }

    private static bool StoreDataToFile(StoreMessage message)
    {
        try
        {
            string fileName = Path.Combine(dataDirectory, $"measurements_{message.DataType}.txt");

            // Formato: timestamp:sensor_id:zona:tipo_dado:valor
            string line = $"{message.Timestamp:O}:{message.SensorId}:{message.Zone}:{message.DataType}:{message.Value}";

            lock (fileLock)
            {
                File.AppendAllText(fileName, line + Environment.NewLine);
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ Erro ao escrever ficheiro: {ex.Message}");
            return false;
        }
    }

    private static void SendResponse(StreamWriter writer, StorageResponseMessage response)
    {
        string json = MessageSerializer.Serialize(response);
        writer.WriteLine(json);
    }
}
