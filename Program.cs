using One_Health.Common.Models;
using One_Health.Common.Protocol;

Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
Console.WriteLine("║   ONE HEALTH - Sistema de Monitorização Urbana            ║");
Console.WriteLine("║   Sistemas Distribuídos - Trabalho Prático nº1           ║");
Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
Console.WriteLine();
Console.WriteLine("Componentes disponíveis:");
Console.WriteLine("  1. Sensor    → dotnet run --project src/Sensor S101 127.0.0.1");
Console.WriteLine("  2. Gateway   → dotnet run --project src/Gateway 9000");
Console.WriteLine("  3. Server    → dotnet run --project src/Server 8000");
Console.WriteLine();
Console.WriteLine("Documentação:");
Console.WriteLine("  • docs/PROTOCOL.md     - Especificação do protocolo");
Console.WriteLine("  • docs/README.md       - Estrutura do projeto");
Console.WriteLine("  • config/sensors.csv   - Configuração dos sensores");
Console.WriteLine();
