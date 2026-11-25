using System.Text.Json;
using System.Text.Json.Serialization;
using Vairas.DocumentManagement.Domain;

namespace Vairas.DocumentManagement.Business;

public static class Wire
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    public static string Save<T>(T value) => JsonSerializer.Serialize(value, Json);
    public static T Read<T>(string json) => JsonSerializer.Deserialize<T>(json, Json) ?? throw DocumentErrors.Dependency("INVALID_STORED_RESULT");
}
