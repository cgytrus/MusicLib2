using System.Text.Json.Serialization;

namespace MusicLib2;

[JsonSourceGenerationOptions(UseStringEnumConverter = true, IgnoreReadOnlyProperties = false)]
[JsonSerializable(typeof(Draft))]
[JsonSerializable(typeof(Extras))]
internal partial class SourceGenerationContext : JsonSerializerContext;
