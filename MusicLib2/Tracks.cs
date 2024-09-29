namespace MusicLib2;

public readonly struct Tracks {
    public IReadOnlyDictionary<string, Track> all { get; }

    private readonly Dictionary<string, Track> _lowercaseTracks = [];

    private static readonly HashSet<string> allowedExtensions = [
        ".mp3",
        ".opus",
        ".ogg",
        ".m4a",
        ".flac",
        ".wav"
    ];

    private Tracks(IReadOnlyDictionary<string, Track> tracks) {
        all = tracks;

        // lowercase tracks for case-insensitive lookups
        foreach ((string fileName, Track track) in tracks)
            _lowercaseTracks.TryAdd(fileName.ToLowerInvariant(), track);
    }

    public bool TryGet(string fileName, out Track track) {
        if (all.TryGetValue(fileName, out track) || _lowercaseTracks.TryGetValue(fileName, out track))
            return true;
        track = new Track {
            title = fileName,
            artist = "",
            links = [],
            error = "track not found"
        };
        return false;
    }

    public static Tracks All() {
        Dictionary<string, Track> tracks = [];
        foreach (string path in Directory.EnumerateFiles(Paths.music)) {
            if (!allowedExtensions.Contains(Path.GetExtension(path)))
                continue;
            tracks.Add(Path.GetFileName(path), Track.FromFile(path));
        }
        return new Tracks(tracks);
    }
}
