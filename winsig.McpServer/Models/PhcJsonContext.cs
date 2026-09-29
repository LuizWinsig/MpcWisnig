using System.Text.Json.Serialization;

namespace winsig.McpServer.Models;

/// <summary>
/// Source-generated JSON context (AOT friendly) for all PHC parameter and response types.
/// Register new tool parameter/DTO types here when adding tools.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    NumberHandling = JsonNumberHandling.AllowReadingFromString)]
[JsonSerializable(typeof(GetRecognizedHoursParameter))]
[JsonSerializable(typeof(GetDetailRecognizedHoursParameter))]
[JsonSerializable(typeof(GetUsersParameter))]
[JsonSerializable(typeof(RecognizedHoursEntry))]
[JsonSerializable(typeof(List<RecognizedHoursEntry>))]
[JsonSerializable(typeof(DetailRecognizedHoursEntry))]
[JsonSerializable(typeof(List<DetailRecognizedHoursEntry>))]
[JsonSerializable(typeof(UserEntry))]
[JsonSerializable(typeof(List<UserEntry>))]
[JsonSerializable(typeof(PhcEnvelope<List<RecognizedHoursEntry>>))]
[JsonSerializable(typeof(PhcEnvelope<List<DetailRecognizedHoursEntry>>))]
[JsonSerializable(typeof(PhcEnvelope<List<UserEntry>>))]
public sealed partial class PhcJsonContext : JsonSerializerContext
{
}
