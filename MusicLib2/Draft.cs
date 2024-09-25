namespace MusicLib2;

public readonly record struct Draft(
    string title,
    string artist,
    string album,
    string albumArtist,
    uint year,
    uint trackNumber,
    uint trackCount,
    uint discNumber,
    uint discCount,
    List<string> links,
    Dictionary<uint, string> files
) {
    public readonly record struct File(
        string link,
        File.Status status,
        string filename,
        uint size, // bytes
        uint length, // seconds
        uint sampleRate, // Hz
        ushort bitrate // Kbps
    ) {
        public enum Status { Downloading, Ready }
    }
}
