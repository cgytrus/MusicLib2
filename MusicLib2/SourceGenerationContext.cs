using System.Text.Json;
using System.Text.Json.Serialization;

namespace MusicLib2;

[JsonSourceGenerationOptions(
    Converters = [typeof(CamelCaseJsonStringEnumConverter)],
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase
)]
[JsonSerializable(typeof(IEnumerable<Track>))]
[JsonSerializable(typeof(IDictionary<int, Playlist>))]
[JsonSerializable(typeof(List<uint>))]
[JsonSerializable(typeof(Dictionary<uint, Draft.File>.KeyCollection))]
[JsonSerializable(typeof(Draft.File.DownloadingDto))]
[JsonSerializable(typeof(Draft.File.ReadyDto))]
[JsonSerializable(typeof(Draft))]
[JsonSerializable(typeof(Dictionary<uint, Draft.File>))]
internal partial class SourceGenerationContext : JsonSerializerContext;

// UseStringEnumConverter doesn't allow me to set the naming policy
// Converters doesn't let me pass constructor arguments directly
// so we do a funny
internal class CamelCaseJsonStringEnumConverter() : JsonStringEnumConverter(JsonNamingPolicy.CamelCase);
