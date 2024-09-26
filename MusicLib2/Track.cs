namespace MusicLib2;

public record struct Track(
    string title,
    string artist,
    string? album,
    string? albumArtist,
    uint year,
    uint trackNumber,
    uint trackCount,
    uint discNumber,
    uint discCount,
    IReadOnlyCollection<string> links
) {
    public static Track FromFile(string path, Extras extras) {
        Track track;
        try {
            using TagLib.File file = TagLib.File.Create(path);
            track = new Track {
                title = file.Tag.Title,
                artist = file.Tag.JoinedPerformers,
                album = file.Tag.Album,
                albumArtist = file.Tag.JoinedAlbumArtists,
                year = file.Tag.Year,
                trackNumber = file.Tag.Track,
                trackCount = file.Tag.TrackCount,
                discNumber = file.Tag.Disc,
                discCount = file.Tag.DiscCount,
                links = []
            };
        }
        catch {
            track = new Track {
                title = Path.GetFileName(path),
                artist = "",
                links = []
            };
        }
        if (extras.links is not null && extras.links.TryGetValue(Path.GetFileName(path), out List<string>? links))
            track.links = links;
        return track;
    }

    public static IEnumerable<Track> AllTracks(bool authorized) {
        Extras extras = Extras.Read();
        return from path in Directory.EnumerateFiles(Paths.music)
            where Path.GetExtension(path) is ".mp3" or ".opus" or ".ogg" or ".m4a" or ".flac" or ".wav"
            let track = FromFile(path, extras)
            where authorized || !string.IsNullOrEmpty(track.artist)
            select track;
    }

    public static IEnumerable<string> AllPlaylists() {
        return from path in Directory.EnumerateFiles(Paths.music)
            where Path.GetExtension(path) is ".m3u" or ".m3u8"
            select Path.GetFileName(path);
    }

    public static IEnumerable<Track> AllFromPlaylist(string path) {
        Extras extras = Extras.Read();
        return from line in File.ReadLines(path)
            where !line.StartsWith('#')
            let trackPath = Path.Join(Paths.music, line)
            where File.Exists(trackPath)
            select FromFile(trackPath, extras);
    }
}
