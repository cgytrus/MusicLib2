using System.Text.Json.Serialization;

namespace MusicLib2;

[JsonSourceGenerationOptions(UseStringEnumConverter = true, IgnoreReadOnlyProperties = false)]
[JsonSerializable(typeof(Draft))]
[JsonSerializable(typeof(Track))]
[JsonSerializable(typeof(Draft.File.DownloadingDto))]
[JsonSerializable(typeof(Draft.File.ReadyDto))]
[JsonSerializable(typeof(Draft.File))]
[JsonSerializable(typeof(Dictionary<uint, Draft.File>))]
internal partial class SourceGenerationContext : JsonSerializerContext;
