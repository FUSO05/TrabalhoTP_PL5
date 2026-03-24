namespace One_Health.Sensor;

/// <summary>
/// Stub para implementação do SENSOR na Fase 2
/// </summary>
internal class SensorProgram
{
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

        string sensorId = args[0];
        string gatewayIp = args[1];
        int gatewayPort = args.Length > 2 ? int.Parse(args[2]) : 9000;

        Console.WriteLine($"Sensor ID: {sensorId}");
        Console.WriteLine($"Gateway: {gatewayIp}:{gatewayPort}");
        Console.WriteLine();
        Console.WriteLine("Aguardando implementação da Fase 2...");
    }
}
