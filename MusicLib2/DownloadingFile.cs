﻿using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Handlers;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using FFMpegCore;
using YoutubeDLSharp;
using YoutubeDLSharp.Options;

namespace MusicLib2;

public partial class DownloadingFile : IProgress<DownloadProgress>, IProgress<string> {
    private static readonly Dictionary<uint, DownloadingFile> downloadingFiles = [];

    public DownloadProgress progress { get; private set; } = new(DownloadState.None);
    public IEnumerable<string> output => _output;

    private readonly uint _id;
    private readonly string _dir;
    private readonly CancellationTokenSource _cts;
    private List<string> _output = [];

    private DownloadingFile(uint id, string dir) {
        _id = id;
        _dir = dir;
        _cts = new CancellationTokenSource();
    }

    public static string? Start(string dir, uint id, string link, bool cobalt) {
        if (downloadingFiles.ContainsKey(id))
            return $"File {id} already downloading.";

        DownloadingFile file = new(id, dir);

        OptionSet overrides = new() {
            RestrictFilenames = false,
            NoRestrictFilenames = true,
            WindowsFilenames = true,
            Mtime = false,
            Proxy = Environment.GetEnvironmentVariable("ML2_PROXY"),
            Cookies = Environment.GetEnvironmentVariable("ML2_COOKIES")
        };

        try {
            if (cobalt) {
                HttpRequestMessage req = new(HttpMethod.Post, "https://api.cobalt.tools/");
                req.Content = JsonContent.Create(new {
                    url = link,
                    downloadMode = "audio",
                    audioFormat = "best",
                    filenameStyle = "nerdy",
                    disableMetadata = true,
                    alwaysProxy = true
                });
                req.Headers.Add("Accept", "application/json");
                file.Report(new DownloadProgress(DownloadState.PreProcessing));
                new HttpClient().SendAsync(req, file._cts.Token).ContinueWith(async task => {
                    try {
                        if (!task.IsCompletedSuccessfully) {
                            file.Report(new DownloadProgress(DownloadState.Error));
                            return;
                        }
                        if (!task.Result.IsSuccessStatusCode) {
                            file.Report(new DownloadProgress(DownloadState.Error, data: task.Result.ReasonPhrase));
                            return;
                        }
                        Dictionary<string, string>? dl =
                            await task.Result.Content.ReadFromJsonAsync<Dictionary<string, string>>(file._cts.Token);
                        if (dl is null) {
                            file.Report(new DownloadProgress(DownloadState.Error, data: "dl is null"));
                            return;
                        }
                        string filePath = Path.Join(dir, FileNameFilterRegex().Replace(dl["filename"], "_"));
                        await using (FileStream f = File.Create(filePath)) {
                            file.Report(new DownloadProgress(DownloadState.Downloading));
                            ProgressMessageHandler progressHandler = new(new HttpClientHandler());
                            progressHandler.HttpReceiveProgress += (_, args) => {
                                float progress = args.BytesTransferred;
                                if (args.TotalBytes.HasValue)
                                    progress /= args.TotalBytes.Value;
                                file.Report(new DownloadProgress(DownloadState.Downloading, progress));
                            };
                            HttpClient client = new(progressHandler);
                            await f.WriteAsync(await client.GetByteArrayAsync(dl["url"], file._cts.Token),
                                file._cts.Token);
                        }
                        file.Report(new DownloadProgress(DownloadState.PostProcessing));
                        IMediaAnalysis analysis = await FFProbe.AnalyseAsync(filePath, null, file._cts.Token);
                        // taglib assumes container format based on extension and
                        // cobalt downloads matroska,webm files from youtube
                        // but sets file extension based on codec, which is usually opus
                        // unlike yt-dlp which correctly chooses webm
                        // remux webm to opus
                        if (analysis.Format.FormatName.EndsWith("webm") &&
                            analysis.PrimaryAudioStream?.CodecName == "opus") {
                            string inFilePath = Path.ChangeExtension(filePath, ".webm");
                            File.Move(filePath, inFilePath);
                            await FFMpegArguments
                                .FromFileInput(inFilePath)
                                .OutputToFile(filePath, true, x => x
                                    .WithCustomArgument("-map 0")
                                    .WithCustomArgument("-c copy"))
                                .CancellableThrough(file._cts.Token)
                                .ProcessAsynchronously(true, new FFOptions {
                                    WorkingDirectory = dir
                                });
                        }
                        file.Report(new DownloadProgress(DownloadState.Success, 1f, data: filePath));
                    }
                    catch (Exception ex) {
                        file.Report(new DownloadProgress(DownloadState.Error, data: ex.ToString()));
                    }
                }, file._cts.Token);
            }
            else {
                YoutubeDL ytdl = new(1);
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    ytdl.YoutubeDLPath = "yt-dlp_linux";

                file.progress = new DownloadProgress(DownloadState.None);
                file._output.Clear();
                ytdl.RunAudioDownload(
                    link,
                    AudioConversionFormat.Best,
                    file._cts.Token,
                    file,
                    file,
                    overrides
                );
            }

            downloadingFiles.Add(id, file);
            return null;
        }
        catch (Exception ex) {
            return ex.ToString();
        }
    }

    public static bool TryGet(uint id, [NotNullWhen(true)] out DownloadingFile? file) =>
        downloadingFiles.TryGetValue(id, out file);
    public static DownloadingFile? Get(uint id) => TryGet(id, out DownloadingFile? file) ? file : null;

    public async Task CancelAsync() {
        await _cts.CancelAsync();
        downloadingFiles.Remove(_id);
    }

    public void Report(DownloadProgress value) {
        if (value.State != DownloadState.Success) {
            progress = value;
            return;
        }
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

    public void Report(string value) {
        _output.Add(value);
    }

    [GeneratedRegex(@"[^a-zA-Z0-9-_.,!()[\] ]")]
    private static partial Regex FileNameFilterRegex();
}
