namespace MusicLib2;

public readonly record struct Library(
    IEnumerable<Track> tracks,
    IReadOnlyDictionary<int, Playlist> playlists
) {
    public static async Task<Library> Get(bool authorized) {
        Tracks tracks = Tracks.All();
        return new Library {
            tracks = tracks.all
                .Where(x => authorized || !string.IsNullOrEmpty(x.Value.artist))
                .OrderBy(x => {
                    try {
                        return File.GetLastWriteTimeUtc(Path.Join(Paths.music, x.Key));
                    }
                    catch {
                        return DateTime.UnixEpoch;
                    }
                })
                .Select(x => x.Value),
            playlists = await Playlist.All(tracks, authorized)
        };
    }
}
