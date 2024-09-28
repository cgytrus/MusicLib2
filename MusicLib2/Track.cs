using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace MusicLib2;

public partial record struct Track(
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
    public static Track FromFile(string path) {
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
                links = string.IsNullOrWhiteSpace(file.Tag.Comment) ? [] : NewLineRegex().Split(file.Tag.Comment)
                    .Where(line => Uri.IsWellFormedUriString(line, UriKind.Absolute)).Distinct().ToImmutableArray()
            };
            if (string.IsNullOrEmpty(track.title))
                track.title = Path.GetFileName(path);
        }
        catch (Exception ex) {
            track = new Track {
                title = Path.GetFileName(path),
                artist = "",
                album = ex.ToString(),
                links = []
            };
        }
        return track;
    }

    [GeneratedRegex("\r\n|\r|\n")]
    private static partial Regex NewLineRegex();
}
