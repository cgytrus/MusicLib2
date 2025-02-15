using System.Text.Json;
using FFMpegCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using YoutubeDLSharp;

namespace MusicLib2;

public record struct Draft(
    string title,
    string artist,
    string? album,
    string? albumArtist,
    uint year,
    uint trackNumber,
    uint trackCount,
    uint discNumber,
    uint discCount,
    List<string> links
) {
    private const int MaxArtSize = 1200;

    public record struct File(
        string link,
        File.Status status,
        string filename
    ) {
        public enum Status { Downloading, Ready }

        public readonly record struct DownloadingDto(
            string link,
            Status status,
            DownloadProgress progress,
            IEnumerable<string> output);

        public readonly record struct ReadyDto(
            string link,
            Status status,
            string format,
            string codec,
            long size, // bytes
            TimeSpan duration, // seconds
            int sampleRate, // Hz
            uint bitrate // bps
        );

        public async Task CancelAndDeleteAsync(string dir, uint id) {
            if (status == Status.Downloading && DownloadingFile.TryGet(id, out DownloadingFile? dl)) {
                await dl.CancelAsync();
            }

            if (string.IsNullOrWhiteSpace(filename))
                return;

            string filePath = Path.Join(dir, filename);
            try {
                if (System.IO.File.Exists(filePath))
                    System.IO.File.Delete(filePath);
            }
            catch { /* ignored */ }

            string spectrogramPath = Path.ChangeExtension(filePath, ".png");
            try {
                if (System.IO.File.Exists(spectrogramPath))
                    System.IO.File.Delete(spectrogramPath);
            }
            catch { /* ignored */ }
        }

        public async Task<ReadyDto> AnalyzeAsync(string dir) {
            string filePath = Path.Join(dir, filename);
            long size = new FileInfo(filePath).Length;
            IMediaAnalysis analysis = await FFProbe.AnalyseAsync(filePath);
            return new ReadyDto {
                link = link,
                status = status,
                format = analysis.Format.FormatName,
                codec = analysis.PrimaryAudioStream?.CodecName ?? "",
                size = size,
                duration = analysis.Duration,
                sampleRate = analysis.PrimaryAudioStream?.SampleRateHz ?? 0,
                bitrate = (uint)analysis.Format.BitRate
            };
        }
    }

    public static async Task CancelAndDeleteAsync(string dir) {
        Dictionary<uint, File> files;

        string filesPath = Path.Join(dir, "files.json");
        if (System.IO.File.Exists(filesPath)) {
            await using FileStream filesFile = System.IO.File.OpenRead(filesPath);
            files = await JsonSerializer.DeserializeAsync(filesFile, SourceGenerationContext.Default.DictionaryUInt32File) ?? [];
        }
        else {
            files = [];
        }

        foreach ((uint fileId, File file) in files) {
            await file.CancelAndDeleteAsync(dir, fileId);
        }

        try {
            if (System.IO.File.Exists(filesPath))
                System.IO.File.Delete(filesPath);
        }
        catch { /* ignored */ }

        try {
            if (System.IO.File.Exists(Path.Join(dir, "meta.json")))
                System.IO.File.Delete(Path.Join(dir, "meta.json"));
        }
        catch { /* ignored */ }

        Directory.Delete(dir, true);
    }

    public static async Task SaveArt(string path, Stream content) {
        using Image image = await Image.LoadAsync(content);
        // resize and crop the image
        image.Mutate(ctx => {
            int width = ctx.GetCurrentSize().Width;
            int height = ctx.GetCurrentSize().Height;
            double aspectRatio = (double)width / height;
            if (width > MaxArtSize) {
                width = MaxArtSize;
                height = (int)Math.Round(width / aspectRatio);
            }
            if (height > MaxArtSize) {
                height = MaxArtSize;
                width = (int)Math.Round(height * aspectRatio);
            }
            if (ctx.GetCurrentSize().Width != width || ctx.GetCurrentSize().Height != height)
                ctx.Resize(width, height);
            int targetCrop = Math.Min(width, height);
            ctx.Crop(new Rectangle((width - targetCrop) / 2, (height - targetCrop) / 2, targetCrop, targetCrop));
        });
        await image.SaveAsJpegAsync(path, new JpegEncoder {
            Quality = 85
        });
    }
}
