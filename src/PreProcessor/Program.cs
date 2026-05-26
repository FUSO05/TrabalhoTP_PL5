using One_Health.PreProcessor.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddGrpc();

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(50051, listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2;
    });
});

var app = builder.Build();
app.MapGrpcService<PreProcessingServiceImpl>();

Console.WriteLine("=== ONE HEALTH PRE-PROCESSOR (gRPC) ===");
Console.WriteLine("Porta: 50051");
Console.WriteLine("Aguardando chamadas RPC do Gateway...");

app.Run();
