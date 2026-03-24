using System.Text.Json;

namespace One_Health.Common.Utilities;

/// <summary>
/// Utilitários para serialização/desserialização de mensagens
/// </summary>
public static class MessageSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static string Serialize<T>(T obj)
    {
        return JsonSerializer.Serialize(obj, Options);
    }

    public static T? Deserialize<T>(string json)
    {
        return JsonSerializer.Deserialize<T>(json, Options);
    }
}
