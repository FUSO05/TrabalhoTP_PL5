namespace One_Health.Gateway;

/// <summary>
/// Stub para implementação do GATEWAY na Fase 2
/// </summary>
internal class GatewayProgram
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== ONE HEALTH GATEWAY ===");
        Console.WriteLine("Fase 2 - Implementação de GATEWAY simples");
        Console.WriteLine();
        
        int port = args.Length > 0 ? int.Parse(args[0]) : 9000;
        string? configFile = args.Length > 1 ? args[1] : null;

        Console.WriteLine($"Porta de escuta: {port}");
        if (!string.IsNullOrEmpty(configFile))
        {
            Console.WriteLine($"Ficheiro de configuração: {configFile}");
        }
        Console.WriteLine();
        Console.WriteLine("Aguardando implementação da Fase 2...");
    }
}
