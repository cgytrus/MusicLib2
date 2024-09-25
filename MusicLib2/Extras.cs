using System.Text.Json;

namespace MusicLib2;

public record struct Extras(
    Dictionary<string, List<string>>? links
) {
    private static readonly string path = Path.Join(ApplicationDbContext.musicDir, "extras.json");

    public static Extras Read() {
        if (!File.Exists(path))
            return new Extras();
        using FileStream file = File.OpenRead(path);
        return JsonSerializer.Deserialize(file, SourceGenerationContext.Default.Extras);
    }
}
