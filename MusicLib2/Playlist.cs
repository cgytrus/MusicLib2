﻿using System.Text;
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
    public enum Type { File, Foobar2000, Poweramp }

    private static async Task<Playlist> FromFile(string path, bool authorized) {
        bool hasUnrecognized = false;
        List<Track> tracks = [];
        StringBuilder paths = new();
        await foreach (string line in File.ReadLinesAsync(path)) {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                continue;
            string filePath = Path.Join(Paths.music, Path.GetFileName(line));
            if (!File.Exists(filePath)) {
                hasUnrecognized = true;
                tracks.Add(new Track {
                    title = line,
                    artist = "",
                    links = []
                });
                paths.Append(line);
                continue;
            }
            Track track = Track.FromFile(filePath);
            if (!authorized && string.IsNullOrEmpty(track.artist))
                continue;
            tracks.Add(track);
            paths.Append(filePath);
        }
        return new Playlist {
            names = new Dictionary<string, List<Type>> { { Path.GetFileNameWithoutExtension(path), [Type.File] } },
            hasUnrecognized = hasUnrecognized,
            tracks = tracks,
            hash = paths.ToString().GetHashCode()
        };
    }

    private static async Task<Playlist> FromFoobar2000(string path, string name, bool authorized) {
        bool hasUnrecognized = false;
        List<Track> tracks = [];
        StringBuilder paths = new();
        await foreach (string line in File.ReadLinesAsync(path)) {
            if (string.IsNullOrWhiteSpace(line))
                continue;
            Uri uri = new(line);
            if (!uri.IsFile) {
                hasUnrecognized = true;
                tracks.Add(new Track {
                    title = line,
                    artist = "",
                    links = []
                });
                paths.Append(line);
                continue;
            }
            string filePath = Path.Join(Paths.music, Path.GetFileName(uri.LocalPath));
            if (!File.Exists(filePath)) {
                hasUnrecognized = true;
                tracks.Add(new Track {
                    title = uri.LocalPath,
                    artist = "",
                    links = []
                });
                paths.Append(uri.LocalPath);
                continue;
            }
            Track track = Track.FromFile(filePath);
            if (!authorized && string.IsNullOrEmpty(track.artist))
                continue;
            tracks.Add(track);
            paths.Append(filePath);
        }
        return new Playlist {
            names = new Dictionary<string, List<Type>> { { name, [Type.Foobar2000] } },
            hasUnrecognized = hasUnrecognized,
            tracks = tracks,
            hash = paths.ToString().GetHashCode()
        };
    }

    // reads my own funny format
    private static async Task<Playlist> FromPoweramp(string path, bool authorized) {
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
            string filePath = Path.Join(Paths.music, Path.GetFileName(line));
            if (!File.Exists(filePath)) {
                hasUnrecognized = true;
                tracks.Add(new Track {
                    title = line,
                    artist = "",
                    links = []
                });
                paths.Append(line);
                continue;
            }
            Track track = Track.FromFile(filePath);
            if (!authorized && string.IsNullOrEmpty(track.artist))
                continue;
            tracks.Add(track);
            paths.Append(filePath);
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

    public static async Task<IDictionary<int, Playlist>> All(bool authorized) {
        Dictionary<int, Playlist> playlists = [];

        await AddOtherPlaylists(playlists, authorized);

        // no foobar2000 playlists
        if (!File.Exists(Paths.foobar2000PlaylistIndex))
            return playlists;

        await AddFoobar2000Playlists(playlists, authorized);

        return playlists;
    }

    private static async Task AddOtherPlaylists(Dictionary<int, Playlist> playlists, bool authorized) {
        foreach (string path in Directory.EnumerateFiles(Paths.music)) {
            string fileName = Path.GetFileName(path);
            if (FilePlaylistRegex().IsMatch(fileName))
                AddPlaylist(playlists, await FromFile(path, authorized));
            else if (PowerampPlaylistRegex().IsMatch(fileName))
                AddPlaylist(playlists, await FromPoweramp(path, authorized));
        }
    }

    private static async Task AddFoobar2000Playlists(Dictionary<int, Playlist> playlists, bool authorized) {
        foreach (string line in await File.ReadAllLinesAsync(Paths.foobar2000PlaylistIndex)) {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            string[] split = line.Split(':', 2);
            if (split.Length != 2)
                continue;

            string guid = split[0];
            string name = split[1];
            if (string.IsNullOrWhiteSpace(guid) || string.IsNullOrWhiteSpace(name))
                continue;

            string path = Path.Join(Paths.foobar2000Playlists, $"playlist-{guid}.fplite");
            if (!File.Exists(path))
                continue;

            AddPlaylist(playlists, await FromFoobar2000(path, name, authorized));
        }
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
}