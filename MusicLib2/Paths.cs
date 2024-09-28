using System.Collections.Immutable;

namespace MusicLib2;

public static class Paths {
    private static readonly bool isDefaultBaseDir = Environment.GetEnvironmentVariable("ML2_PATH") is null;
    private static readonly bool isDefaultBaseReadDir = isDefaultBaseDir &&
        Environment.GetEnvironmentVariable("ML2_READ_PATH") is null;

    public static readonly string baseDir = Environment.GetEnvironmentVariable("ML2_PATH") ??
        Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MusicLib2");
    public static readonly string baseReadDir = Environment.GetEnvironmentVariable("ML2_READ_PATH") ??
        Environment.GetEnvironmentVariable("ML2_PATH") ??
        Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MusicLib2");

    public static readonly string music = isDefaultBaseReadDir ?
        Environment.GetFolderPath(Environment.SpecialFolder.MyMusic) :
        Path.Join(baseReadDir, "music");

    public static readonly string foobar2000Playlists = isDefaultBaseReadDir ?
        Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "foobar2000-v2", "playlists-v2.0") :
        Path.Join(baseReadDir, "foobar2000");

    public static readonly string drafts = Path.Join(baseDir, "drafts");

    //public static readonly string cacheDb = Path.Join(baseDir, "cache.db");
    public static readonly string auth = Path.Join(baseReadDir, "auth.txt");

    public static readonly string foobar2000PlaylistIndex = Path.Join(foobar2000Playlists, "index.txt");

    // TODO: idk where to move this .......
    public static ImmutableHashSet<string> tokens { get; private set; } = ImmutableHashSet<string>.Empty;

    static Paths() {
        if (!Directory.Exists(baseDir))
            Directory.CreateDirectory(baseDir);
        if (!Directory.Exists(music))
            Directory.CreateDirectory(music);
        if (!Directory.Exists(foobar2000Playlists))
            Directory.CreateDirectory(foobar2000Playlists);
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
