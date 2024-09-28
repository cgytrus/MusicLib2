﻿namespace MusicLib2;

public readonly record struct Library(
    IEnumerable<Track> tracks,
    IReadOnlyDictionary<int, Playlist> playlists
) {
    public static async Task<Library> Get(bool authorized) {
        Tracks tracks = Tracks.All();
        return new Library {
            tracks = tracks.all.Values.Where(x => authorized || !string.IsNullOrEmpty(x.artist)),
            playlists = await Playlist.All(tracks, authorized)
        };
    }
}
