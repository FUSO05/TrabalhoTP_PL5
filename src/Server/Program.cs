namespace One_Health.Server;

/// <summary>
/// Stub para implementação do SERVIDOR na Fase 2
/// </summary>
internal class ServerProgram
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== ONE HEALTH SERVER ===");
        Console.WriteLine("Fase 2 - Implementação de SERVIDOR simples");
        Console.WriteLine();
        
        int port = args.Length > 0 ? int.Parse(args[0]) : 8000;
        string? dataDirectory = args.Length > 1 ? args[1] : "./data";

        Console.WriteLine($"Porta de escuta: {port}");
        Console.WriteLine($"Diretório de dados: {dataDirectory}");
        Console.WriteLine();
        Console.WriteLine("Aguardando implementação da Fase 2...");
    }
}
