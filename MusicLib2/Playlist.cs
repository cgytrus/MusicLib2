using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace MusicLib2;

public partial record struct Playlist(
    Dictionary<string, List<Playlist.Type>> names,
    bool hasUnrecognized,
    IEnumerable<Track> tracks,
    [property: JsonIgnore]
    int hash
) {
    public enum Type { File, Poweramp, MusicBee }

    private static async Task<Playlist> FromFile(Tracks allTracks, string path, bool authorized) {
        bool hasUnrecognized = false;
        List<Track> tracks = [];
        StringBuilder paths = new();
        await foreach (string line in File.ReadLinesAsync(path)) {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                continue;
            string fileName = Path.GetFileName(line);
            if (!allTracks.TryGet(fileName, out Track track)) {
                hasUnrecognized = true;
                track.title = line;
                tracks.Add(track);
                paths.Append(line);
                continue;
            }
            if (!authorized && track.error is not null)
                continue;
            tracks.Add(track);
            paths.Append(fileName);
        }
        return new Playlist {
            names = new Dictionary<string, List<Type>> { { Path.GetFileNameWithoutExtension(path), [Type.File] } },
            hasUnrecognized = hasUnrecognized,
            tracks = tracks,
            hash = paths.ToString().GetHashCode()
        };
    }

    // reads my own funny format
    private static async Task<Playlist> FromPoweramp(Tracks allTracks, string path, bool authorized) {
        string? name = null;
        bool hasUnrecognized = false;
        List<Track> tracks = [];
        StringBuilder paths = new();
        await foreach (string line in File.ReadLinesAsync(path)) {
            if (string.IsNullOrWhiteSpace(line))
                continue;
            if (name is null) {
                name = line;
                continue;
            }
            string fileName = Path.GetFileName(line);
            if (!allTracks.TryGet(fileName, out Track track)) {
                hasUnrecognized = true;
                track.title = line;
                tracks.Add(track);
                paths.Append(line);
                continue;
            }
            if (!authorized && track.error is not null)
                continue;
            tracks.Add(track);
            paths.Append(fileName);
        }
        return new Playlist {
            names = new Dictionary<string, List<Type>> {
                { name ?? Path.GetFileNameWithoutExtension(path), [Type.Poweramp] }
            },
            hasUnrecognized = hasUnrecognized,
            tracks = tracks,
            hash = paths.ToString().GetHashCode()
        };
    }

    // reads reverse engineered musicbee's proprietary format
    private static async Task<Playlist> FromMusicBee(Tracks allTracks, string path, bool authorized) {
        bool hasUnrecognized = false;
        List<Track> tracks = [];
        StringBuilder paths = new();
        await using FileStream file = File.OpenRead(path);
        using BinaryReader reader = new(file);

        MusicBeePreRead(reader);
        int trackCount = reader.ReadInt32();
        for (int i = 0; i < trackCount; i++) {
            string filePath = reader.ReadString();
            string fileName = filePath.Split('\\').LastOrDefault("");

            MusicBeeReadEmbedded(reader);

            if (!allTracks.TryGet(fileName, out Track track)) {
                hasUnrecognized = true;
                track.title = filePath;
                tracks.Add(track);
                paths.Append(filePath);
                continue;
            }
            if (!authorized && track.error is not null)
                continue;
            tracks.Add(track);
            paths.Append(fileName);
        }

        return new Playlist {
            names = new Dictionary<string, List<Type>> { { Path.GetFileNameWithoutExtension(path), [Type.MusicBee] } },
            hasUnrecognized = hasUnrecognized,
            tracks = tracks,
            hash = paths.ToString().GetHashCode()
        };
    }

    private static void MusicBeePreRead(BinaryReader reader) {
        reader.ReadByte(); // version
        reader.ReadByte();
        reader.ReadByte();
        reader.ReadByte(); // padding
        reader.ReadString();
        reader.ReadByte();
        reader.ReadBoolean();
        int a = reader.ReadInt32();
        for (int i = 0; i < a; i++) {
            reader.ReadString();
            int b = reader.ReadInt32();
            for (int j = 0; j < b; j++) {
                reader.ReadByte();
                reader.ReadInt32();
            }
        }
        // TODO: maybe actually use this someday?
        /*
         * enum PlaylistSorting {
         *     None, ArtistAlbum, GenreArtistAlbum, YearArtistAlbum, YearAlbum, Album, Custom1, Custom2, Custom3,
         *     Custom4, Rank, Artist, Custom5, Title, Custom6, Custom7, Custom8, ArtistYearAlbum, Random,
         *     AlbumDateAdded, OrigYearAlbum, Ascending = 0, Descending = 128
         * }
         */
        reader.ReadInt32();
        a = reader.ReadInt32();
        for (int i = 1; i < a; i++) {
            reader.ReadByte();
            reader.ReadInt32();
        }
    }

    private static void MusicBeeReadEmbedded(BinaryReader reader) {
        if (reader.ReadInt32() < int.MaxValue)
            return;
        // we don't care about its contents
        reader.ReadString(); // title
        reader.ReadString(); // artist
        reader.ReadString(); // album artist
        reader.ReadString(); // album
        reader.ReadString(); // disc number
        reader.ReadString(); // track number
        reader.ReadInt32(); // rating?
        reader.ReadInt32(); // duration?
        reader.ReadString(); // album cover
        if (reader.ReadBoolean()) {
            reader.ReadString(); // file temp id
            reader.ReadString(); // playback start time
            reader.ReadString(); // playback end time
        }
        reader.ReadString(); // unused?
    }

    public static async Task<IReadOnlyDictionary<int, Playlist>> All(Tracks allTracks, bool authorized) {
        Dictionary<int, Playlist> playlists = [];
        foreach (string path in Directory.EnumerateFiles(Paths.music)) {
            string fileName = Path.GetFileName(path);
            if (FilePlaylistRegex().IsMatch(fileName))
                AddPlaylist(playlists, await FromFile(allTracks, path, authorized));
            else if (PowerampPlaylistRegex().IsMatch(fileName))
                AddPlaylist(playlists, await FromPoweramp(allTracks, path, authorized));
            else if (MusicBeePlaylistRegex().IsMatch(fileName))
                AddPlaylist(playlists, await FromMusicBee(allTracks, path, authorized));
        }
        return playlists;
    }

    private static void AddPlaylist(Dictionary<int, Playlist> playlists, Playlist playlist) {
        if (!playlists.TryGetValue(playlist.hash, out Playlist existing)) {
            playlists.Add(playlist.hash, playlist);
            return;
        }
        foreach ((string name, List<Type> types) in playlist.names) {
            if (existing.names.TryGetValue(name, out List<Type>? existingTypes))
                existingTypes.AddRange(types);
            else
                existing.names.Add(name, types);
        }
    }

    [GeneratedRegex(@".*\.m3u8?")]
    private static partial Regex FilePlaylistRegex();

    [GeneratedRegex(@"poweramp[0-9]+\.txt")]
    private static partial Regex PowerampPlaylistRegex();

    [GeneratedRegex(@".*\.mbp")]
    private static partial Regex MusicBeePlaylistRegex();
}
