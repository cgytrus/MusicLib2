using System.Collections.Immutable;

namespace MusicLib2;

public static class Paths {
    public static readonly string baseDir = Environment.GetEnvironmentVariable("ML2_PATH") ??
        Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MusicLib2");
    public static readonly string musicDir = Environment.GetEnvironmentVariable("ML2_PATH") is null ?
        Environment.GetFolderPath(Environment.SpecialFolder.MyMusic) : Path.Join(baseDir, "music");
    public static readonly string draftDir = Path.Join(baseDir, "drafts");

    //public static readonly string cacheDb = Path.Join(baseDir, "cache.db");
    public static readonly string authFile = Path.Join(baseDir, "auth.txt");
    public static readonly string extrasFile = Path.Join(musicDir, "extras.json");

    // TODO: idk where to move this .......
    public static ImmutableHashSet<string> tokens { get; private set; } = ImmutableHashSet<string>.Empty;

    static Paths() {
        if (!Directory.Exists(baseDir))
            Directory.CreateDirectory(baseDir);
        if (!Directory.Exists(musicDir))
            Directory.CreateDirectory(musicDir);
        if (!Directory.Exists(draftDir))
            Directory.CreateDirectory(draftDir);
    }

    public static void RefreshAuth() {
        if (!File.Exists(authFile)) {
            tokens = ImmutableHashSet<string>.Empty;
            return;
        }
        tokens = File.ReadLines(authFile).ToImmutableHashSet();
    }
}
