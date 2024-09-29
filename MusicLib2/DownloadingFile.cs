using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text.Json;
using FFMpegCore;
using YoutubeDLSharp;
using YoutubeDLSharp.Options;

namespace MusicLib2;

public class DownloadingFile : IProgress<DownloadProgress> {
    private static readonly Dictionary<uint, DownloadingFile> downloadingFiles = [];

    public DownloadProgress progress { get; private set; } = new(DownloadState.None);

    private readonly uint _id;
    private readonly string _dir;
    private readonly CancellationTokenSource _cts;
    private Process? _vpn = null;

    private DownloadingFile(uint id, string dir) {
        _id = id;
        _dir = dir;
        _cts = new CancellationTokenSource();
    }

    public static string? Start(string dir, uint id, string link, string? proxy) {
        if (downloadingFiles.ContainsKey(id))
            return $"File {id} already downloading.";

        DownloadingFile file = new(id, dir);

        OptionSet overrides = OptionSet.Default;
        overrides.WindowsFilenames = true;

        try {
            YoutubeDL ytdl = new(1) {
                RestrictFilenames = true
            };
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                ytdl.YoutubeDLPath = "yt-dlp_linux";

            if (!string.IsNullOrWhiteSpace(proxy)) {
                ProcessStartInfo startInfo = new() {
                    FileName = Environment.GetEnvironmentVariable("ML2_PROXY_PATH"),
                    Arguments = $"-c '{Path.Join(Paths.baseReadDir, proxy)}'"
                };
                file._vpn = Process.Start(startInfo);
                overrides.Proxy = Environment.GetEnvironmentVariable("ML2_PROXY");
            }
            ytdl.RunAudioDownload(
                link,
                AudioConversionFormat.Best,
                file._cts.Token,
                file,
                null,
                overrides
            );

            downloadingFiles.Add(id, file);
            return null;
        }
        catch (Exception ex) {
            try { file._vpn?.Kill(); }
            catch { /* ignored*/ }
            return ex.ToString();
        }
    }

    public static bool TryGet(uint id, [NotNullWhen(true)] out DownloadingFile? file) =>
        downloadingFiles.TryGetValue(id, out file);

    public async Task CancelAsync() {
        await _cts.CancelAsync();
        downloadingFiles.Remove(_id);
        try { _vpn?.Kill(); }
        catch { /* ignored*/ }
    }

    public void Report(DownloadProgress value) {
        if (value.State != DownloadState.Success) {
            progress = value;
            return;
        }
        try { _vpn?.Kill(); }
        catch { /* ignored*/ }
        if (!Directory.Exists(_dir)) {
            progress = new DownloadProgress(DownloadState.Error, data: "Draft does not exist.");
            return;
        }
        string filesPath = Path.Join(_dir, "files.json");
        if (!File.Exists(filesPath)) {
            progress = new DownloadProgress(DownloadState.Error, data: "Draft metadata does not exist?");
            return;
        }

        try {
            Dictionary<uint, Draft.File> files;
            using (FileStream metaFile = File.OpenRead(filesPath)) {
                files = JsonSerializer.Deserialize(metaFile, SourceGenerationContext.Default.DictionaryUInt32File) ?? [];
            }

            if (!files.TryGetValue(_id, out Draft.File file)) {
                progress = new DownloadProgress(DownloadState.Error, data: "File does not exist in draft.");
                return;
            }

            string fileName = Path.GetFileName(value.Data);
            string path = Path.Join(_dir, fileName);
            File.Move(value.Data, path);

            string spectrogramPath = Path.ChangeExtension(path, ".png");
            FFMpegArguments
                .FromFileInput(path)
                .OutputToFile(spectrogramPath, true, x => x
                    .WithCustomArgument("-lavfi showspectrumpic=s=1024x128:legend=disabled"))
                .ProcessSynchronously(true, new FFOptions {
                    WorkingDirectory = _dir
                });

            file.status = Draft.File.Status.Ready;
            file.filename = fileName;
            files[_id] = file;

            using (FileStream metaFile = File.Create(filesPath)) {
                JsonSerializer.Serialize(metaFile, files, SourceGenerationContext.Default.DictionaryUInt32File);
            }

            progress = value;
            downloadingFiles.Remove(_id);
        }
        catch (Exception ex) {
            progress = new DownloadProgress(DownloadState.Error, data: ex.ToString());
        }
    }
}
