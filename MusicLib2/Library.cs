namespace MusicLib2;

public readonly record struct Library(
    IEnumerable<Track> tracks,
    IReadOnlyDictionary<int, Playlist> playlists,
    IEnumerable<string> errors
) {
    public static async Task<Library> Get(bool authorized) {
        Tracks allTracks = Tracks.All();
        List<Track> tracks = allTracks.all
            .Where(x => authorized || x.Value.error is null)
            .OrderByDescending(x => {
                try {
                    return File.GetLastWriteTimeUtc(Path.Join(Paths.music, x.Key));
                }
                catch {
                    return DateTime.UnixEpoch;
                }
            })
            .Select(x => x.Value).ToList();
        IReadOnlyDictionary<int, Playlist> playlists = await Playlist.All(allTracks, authorized);
        return new Library {
            tracks = tracks,
            playlists = playlists,
            errors = allTracks.all
                .Where(x => x.Value.error is not null)
                .Select(x => $"{x.Key} - {x.Value.error}")
                .Concat(playlists
                    .SelectMany(x => x.Value.tracks
                        .Where(y => y.error is not null)
                        .Select(y => $"{x.Value.names.Keys.FirstOrDefault(x.Key.ToString())} - {y.title} - {y.error}")
                    )
                )
        };
    }
}
