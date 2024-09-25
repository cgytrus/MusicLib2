using System.Text.Json;

namespace MusicLib2;

public record struct Extras(
    Dictionary<string, List<string>>? links
) {
    public static Extras Read() {
        if (!File.Exists(Paths.extrasFile))
            return new Extras();
        using FileStream file = File.OpenRead(Paths.extrasFile);
        return JsonSerializer.Deserialize(file, SourceGenerationContext.Default.Extras);
    }
}
