using System.Collections.Immutable;

namespace MusicLib2;

public static class Paths {
    public static readonly string baseDir = Environment.GetEnvironmentVariable("ML2_PATH") ??
        Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MusicLib2");
    public static readonly string music = Environment.GetEnvironmentVariable("ML2_PATH") is null ?
        Environment.GetFolderPath(Environment.SpecialFolder.MyMusic) : Path.Join(baseDir, "music");
    public static readonly string drafts = Path.Join(baseDir, "drafts");

    //public static readonly string cacheDb = Path.Join(baseDir, "cache.db");
    public static readonly string auth = Path.Join(baseDir, "auth.txt");
    public static readonly string extras = Path.Join(music, "extras.json");

    // TODO: idk where to move this .......
    public static ImmutableHashSet<string> tokens { get; private set; } = ImmutableHashSet<string>.Empty;

    static Paths() {
        if (!Directory.Exists(baseDir))
            Directory.CreateDirectory(baseDir);
        if (!Directory.Exists(music))
            Directory.CreateDirectory(music);
        if (!Directory.Exists(drafts))
            Directory.CreateDirectory(drafts);
    }

    public static void RefreshAuth() {
        if (!File.Exists(auth)) {
            tokens = ImmutableHashSet<string>.Empty;
            return;
        }
        tokens = File.ReadLines(auth).ToImmutableHashSet();
    }
}
