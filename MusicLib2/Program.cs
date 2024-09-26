using System.Runtime.InteropServices;
using System.Text.Json;
using FFMpegCore;
using MusicLib2;
using TagLib;
using YoutubeDLSharp;
using File = System.IO.File;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options => {
    options.AddDefaultPolicy(policy => {
        policy.AllowAnyOrigin();
        policy.AllowAnyHeader();
        policy.AllowAnyMethod();
        policy.WithExposedHeaders("Content-Disposition");
    });
});

WebApplication app = builder.Build();

app.UsePathBase("/music");

if (app.Environment.IsDevelopment()) {
    app.UseSwagger();
    app.UseSwaggerUI();
}
else {
    // nugert my beloved
    if (!File.Exists(Path.Combine(Utils.FfmpegBinaryName)))
        await Utils.DownloadFFmpeg();
    if (!File.Exists(Path.Combine(Utils.FfprobeBinaryName)))
        await Utils.DownloadFFprobe();
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
        if (!File.Exists(Path.Combine("yt-dlp_linux"))) {
            const string ytdlpDownloadUrl = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp_linux";
            File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(), Path.GetFileName(ytdlpDownloadUrl)),
                await new HttpClient().GetByteArrayAsync(ytdlpDownloadUrl));
        }
        string ytdlpPath = Utils.GetFullPath("yt-dlp_linux");
        string ffmpegPath = Utils.GetFullPath(Utils.FfmpegBinaryName);
        string ffprobePath = Utils.GetFullPath(Utils.FfprobeBinaryName);
        if (File.Exists(ytdlpPath))
            File.SetUnixFileMode(ytdlpPath, File.GetUnixFileMode(ytdlpPath) | UnixFileMode.UserExecute);
        if (File.Exists(ffmpegPath))
            File.SetUnixFileMode(ffmpegPath, File.GetUnixFileMode(ffmpegPath) | UnixFileMode.UserExecute);
        if (File.Exists(ffprobePath))
            File.SetUnixFileMode(ffprobePath, File.GetUnixFileMode(ffprobePath) | UnixFileMode.UserExecute);
    }
    else {
        if (!File.Exists(Path.Combine(Utils.YtDlpBinaryName)))
            await Utils.DownloadYtDlp();
    }
}

app.UseCors();

RouteGroupBuilder baseGroup = app.MapGroup("/v2");

baseGroup.MapGet("/auth", (HttpContext ctx) => {
    Paths.RefreshAuth();
    return !TryAuthorize(ctx) ? Results.Text("Invalid token") : Results.Text("");
}).WithOpenApi();

baseGroup.MapGet("/tracks", (HttpContext ctx) => Results.Json(Track.AllTracks(TryAuthorize(ctx)))).WithOpenApi();

baseGroup.MapGet("/playlists", () => Results.Json(Track.AllPlaylists())).WithOpenApi();

baseGroup.MapGet("/playlist/{fileName}", (HttpContext ctx, string fileName) => {
    if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        return Results.BadRequest("Filename contains invalid character.");
    string path = Path.Join(Paths.music, fileName);
    return !File.Exists(path) ?
        Results.NotFound("Playlist not found.") :
        Results.Json(Track.AllFromPlaylist(path, TryAuthorize(ctx)));
}).WithOpenApi();

RouteGroupBuilder draftGroup = baseGroup.MapGroup("/draft");

baseGroup.MapGet("/drafts", (HttpContext ctx) => {
    if (!TryAuthorize(ctx))
        return Results.Unauthorized();
    List<uint> drafts = [];
    foreach (string path in Directory.EnumerateDirectories(Paths.drafts)) {
        string name = Path.GetFileName(path);
        if (uint.TryParse(name, out uint id))
            drafts.Add(id);
    }
    return Results.Json(drafts);
}).WithOpenApi();

draftGroup.MapPost("/", async (HttpContext ctx) => {
    if (!TryAuthorize(ctx))
        return Results.Unauthorized();
    uint id = 1;
    foreach (string entry in Directory.EnumerateFileSystemEntries(Paths.drafts)) {
        if (uint.TryParse(entry, out uint num) && num >= id)
            id = num + 1;
    }
    string dir = Path.Join(Paths.drafts, id.ToString());
    Directory.CreateDirectory(dir);
    Draft draft = new() {
        title = "",
        artist = "",
        album = null,
        albumArtist = null,
        year = 0,
        trackNumber = 1,
        trackCount = 1,
        discNumber = 1,
        discCount = 1,
        links = []
    };
    await using (FileStream file = File.Create(Path.Join(dir, "meta.json"))) {
        await JsonSerializer.SerializeAsync(file, draft, SourceGenerationContext.Default.Draft);
    }
    await using (FileStream file = File.Create(Path.Join(dir, "files.json"))) {
        await JsonSerializer.SerializeAsync(file, new Dictionary<uint, Draft.File>(),
            SourceGenerationContext.Default.DictionaryUInt32File);
    }
    return Results.Created($"/draft/{id}", id);
}).WithOpenApi();

draftGroup.MapGet("/{draftId}/meta", async (HttpContext ctx, uint draftId) => {
    if (!TryAuthorize(ctx))
        return Results.Unauthorized();
    string dir = Path.Join(Paths.drafts, draftId.ToString());
    if (!Directory.Exists(dir))
        return Results.NotFound("Draft does not exist.");
    string meta = Path.Join(dir, "meta.json");
    if (!File.Exists(meta))
        return Results.NotFound("Draft metadata does not exist?");
    return Results.Text(await File.ReadAllTextAsync(meta), "application/json");
}).WithOpenApi();

draftGroup.MapPut("/{draftId}/meta", async (HttpContext ctx, uint draftId) => {
    if (!TryAuthorize(ctx))
        return Results.Unauthorized();
    string dir = Path.Join(Paths.drafts, draftId.ToString());
    if (!Directory.Exists(dir))
        return Results.NotFound("Draft does not exist.");
    string meta = Path.Join(dir, "meta.json");
    Draft draft;
    try {
        draft = await JsonSerializer.DeserializeAsync(ctx.Request.Body, SourceGenerationContext.Default.Draft);
    }
    catch (JsonException ex) {
        return Results.BadRequest($"Input JSON data invalid. {ex.Message}");
    }
    await using (FileStream file = File.Create(meta)) {
        await JsonSerializer.SerializeAsync(file, draft, SourceGenerationContext.Default.Draft);
    }
    return Results.NoContent();
}).WithOpenApi();

draftGroup.MapPut("/{draftId}/art", async (HttpContext ctx, uint draftId) => {
    if (!TryAuthorize(ctx))
        return Results.Unauthorized();
    if (ctx.Request.ContentType is null)
        return Results.BadRequest("Content type required.");
    string dir = Path.Join(Paths.drafts, draftId.ToString());
    if (!Directory.Exists(dir))
        return Results.NotFound("Draft does not exist.");
    if (ctx.Request.ContentType.StartsWith("text/")) {
        string link;
        using (StreamReader reader = new(ctx.Request.Body)) {
            link = await reader.ReadToEndAsync();
        }
        if (!Uri.IsWellFormedUriString(link, UriKind.Absolute))
            return Results.BadRequest("Invalid URI.");
        await Draft.SaveArt(Path.Join(dir, "art.jpg"), await new HttpClient().GetStreamAsync(link));
    }
    else {
        await Draft.SaveArt(Path.Join(dir, "art.jpg"), ctx.Request.Body);
    }
    return Results.NoContent();
}).WithOpenApi();

draftGroup.MapGet("/{draftId}/files", async (HttpContext ctx, uint draftId) => {
    if (!TryAuthorize(ctx))
        return Results.Unauthorized();
    string dir = Path.Join(Paths.drafts, draftId.ToString());
    if (!Directory.Exists(dir))
        return Results.NotFound("Draft does not exist.");
    string filesPath = Path.Join(dir, "files.json");
    if (!File.Exists(filesPath))
        return Results.NotFound("Draft files does not exist?");
    Dictionary<uint, Draft.File> files;
    await using (FileStream filesFile = File.OpenRead(filesPath)) {
        files = await JsonSerializer.DeserializeAsync(filesFile, SourceGenerationContext.Default.DictionaryUInt32File) ?? [];
    }
    return Results.Json(files.Keys);
}).WithOpenApi();

draftGroup.MapPost("/{draftId}/file", async (HttpContext ctx, uint draftId) => {
    if (!TryAuthorize(ctx))
        return Results.Unauthorized();
    string dir = Path.Join(Paths.drafts, draftId.ToString());
    if (!Directory.Exists(dir))
        return Results.NotFound("Draft does not exist.");
    string filesPath = Path.Join(dir, "files.json");
    if (!File.Exists(filesPath))
        return Results.NotFound("Draft files does not exist?");
    string link;
    using (StreamReader reader = new(ctx.Request.Body)) {
        link = await reader.ReadToEndAsync();
    }
    if (!Uri.IsWellFormedUriString(link, UriKind.Absolute))
        return Results.BadRequest("Invalid URI.");

    Dictionary<uint, Draft.File> files;
    await using (FileStream file = File.OpenRead(filesPath)) {
        files = await JsonSerializer.DeserializeAsync(file, SourceGenerationContext.Default.DictionaryUInt32File) ?? [];
    }

    uint fileId = files.Count == 0 ? 1 : files.Keys.Max() + 1;
    files[fileId] = new Draft.File(link, Draft.File.Status.Downloading, "");

    await using (FileStream file = File.Create(filesPath)) {
        await JsonSerializer.SerializeAsync(file, files, SourceGenerationContext.Default.DictionaryUInt32File);
    }

    string? error = DownloadingFile.Start(dir, fileId, link);
    return error is null ?
        Results.Created($"/draft/{draftId}/file/{fileId}", fileId) :
        Results.Problem(error, null, 500, "Failed to start download");
}).WithOpenApi();

draftGroup.MapGet("/{draftId}/file/{fileId}", async (HttpContext ctx, uint draftId, uint fileId) => {
    if (!TryAuthorize(ctx))
        return Results.Unauthorized();
    string dir = Path.Join(Paths.drafts, draftId.ToString());
    if (!Directory.Exists(dir))
        return Results.NotFound("Draft does not exist.");
    string filesPath = Path.Join(dir, "files.json");
    if (!File.Exists(filesPath))
        return Results.NotFound("Draft files does not exist?");

    Dictionary<uint, Draft.File> files;
    await using (FileStream filesFile = File.OpenRead(filesPath)) {
        files = await JsonSerializer.DeserializeAsync(filesFile, SourceGenerationContext.Default.DictionaryUInt32File) ?? [];
    }

    if (!files.TryGetValue(fileId, out Draft.File file))
        return Results.NotFound("Draft file does not exist.");

    return file.status switch {
        Draft.File.Status.Downloading => DownloadingFile.TryGet(fileId, out DownloadingFile? dl) ?
            Results.Json(new Draft.File.DownloadingDto(file.link, file.status, dl.progress),
                SourceGenerationContext.Default.DownloadingDto) : Results.NotFound("Downloading file does not exist."),
        Draft.File.Status.Ready => Results.Json(await file.AnalyzeAsync(dir), SourceGenerationContext.Default.ReadyDto),
        _ => Results.Problem(file.status.ToString(), null, 500, "Unknown file status.")
    };
}).WithOpenApi();

draftGroup.MapGet("/{draftId}/file/{fileId}/spectrogram", async (HttpContext ctx, uint draftId, uint fileId) => {
    if (!TryAuthorize(ctx))
        return Results.Unauthorized();
    string dir = Path.Join(Paths.drafts, draftId.ToString());
    if (!Directory.Exists(dir))
        return Results.NotFound("Draft does not exist.");
    string filesPath = Path.Join(dir, "files.json");
    if (!File.Exists(filesPath))
        return Results.NotFound("Draft files does not exist?");

    Dictionary<uint, Draft.File> files;
    await using (FileStream filesFile = File.OpenRead(filesPath)) {
        files = await JsonSerializer.DeserializeAsync(filesFile, SourceGenerationContext.Default.DictionaryUInt32File) ?? [];
    }

    if (!files.TryGetValue(fileId, out Draft.File file))
        return Results.NotFound("Draft file does not exist.");
    if (file.status != Draft.File.Status.Ready)
        return Results.BadRequest("Draft file is not ready.");

    string spectrogramPath = Path.ChangeExtension(Path.Join(dir, file.filename), ".png");
    if (!File.Exists(spectrogramPath))
        return Results.NotFound("Spectrogram does not exist.");

    return Results.File(spectrogramPath, "image/png");
}).WithOpenApi();

draftGroup.MapDelete("/{draftId}/file/{fileId}", async (HttpContext ctx, uint draftId, uint fileId) => {
    if (!TryAuthorize(ctx))
        return Results.Unauthorized();
    string dir = Path.Join(Paths.drafts, draftId.ToString());
    if (!Directory.Exists(dir))
        return Results.NotFound("Draft does not exist.");
    string filesPath = Path.Join(dir, "files.json");
    if (!File.Exists(filesPath))
        return Results.NotFound("Draft files does not exist?");

    Dictionary<uint, Draft.File> files;
    await using (FileStream filesFile = File.OpenRead(filesPath)) {
        files = await JsonSerializer.DeserializeAsync(filesFile, SourceGenerationContext.Default.DictionaryUInt32File) ?? [];
    }

    if (!files.TryGetValue(fileId, out Draft.File file))
        return Results.NotFound("Draft file does not exist.");

    if (file.status == Draft.File.Status.Downloading && DownloadingFile.TryGet(fileId, out DownloadingFile? dl)) {
        await dl.CancelAsync();
    }

    if (string.IsNullOrWhiteSpace(file.filename))
        return Results.NoContent();

    string filePath = Path.Join(dir, file.filename);
    try {
        if (File.Exists(filePath))
            File.Delete(filePath);
    }
    catch { /* ignored */ }

    string spectrogramPath = Path.ChangeExtension(filePath, ".png");
    try {
        if (File.Exists(spectrogramPath))
            File.Delete(spectrogramPath);
    }
    catch { /* ignored */ }

    files.Remove(fileId);
    await using (FileStream filesFile = File.Create(filesPath)) {
        await JsonSerializer.SerializeAsync(filesFile, files, SourceGenerationContext.Default.DictionaryUInt32File);
    }
    return Results.NoContent();
}).WithOpenApi();

draftGroup.MapPost("/{draftId}/finalize/{fileId}", async (HttpContext ctx, uint draftId, uint fileId) => {
    if (!TryAuthorize(ctx))
        return Results.Unauthorized();
    string dir = Path.Join(Paths.drafts, draftId.ToString());
    if (!Directory.Exists(dir))
        return Results.NotFound("Draft does not exist.");

    string metaPath = Path.Join(dir, "meta.json");
    if (!File.Exists(metaPath))
        return Results.NotFound("Draft metadata does not exist?");

    string filesPath = Path.Join(dir, "files.json");
    if (!File.Exists(filesPath))
        return Results.NotFound("Draft files does not exist?");

    string artPath = Path.Join(dir, "art.jpg");
    if (!File.Exists(artPath))
        return Results.NotFound("Draft art missing.");

    Dictionary<uint, Draft.File> files;
    await using (FileStream filesFile = File.OpenRead(filesPath)) {
        files = await JsonSerializer.DeserializeAsync(filesFile, SourceGenerationContext.Default.DictionaryUInt32File) ?? [];
    }

    if (!files.TryGetValue(fileId, out Draft.File file))
        return Results.NotFound("Draft file does not exist.");
    if (file.status != Draft.File.Status.Ready)
        return Results.BadRequest("Draft file is not ready.");

    Draft draft;
    await using (FileStream metaFile = File.OpenRead(metaPath)) {
        draft = await JsonSerializer.DeserializeAsync(metaFile, SourceGenerationContext.Default.Draft);
    }

    string filePath = Path.Join(dir, file.filename);

    TagLib.File? tags;
    try { tags = TagLib.File.Create(filePath); }
    catch (Exception ex) {
        return Results.Problem(ex.ToString(), null, 500, "Error reading metadata.");
    }
    if (tags is null) {
        return Results.Problem("file is null", null, 500, "Error reading metadata.");
    }

    try {
        string links = string.Join('\n', draft.links);
        tags.Tag.Title = draft.title;
        tags.Tag.Performers = [draft.artist];
        tags.Tag.Album = draft.album ?? draft.title;
        tags.Tag.AlbumArtists = [draft.albumArtist ?? draft.artist];
        tags.Tag.Year = draft.year;
        tags.Tag.Pictures = [new Picture(artPath)];
        tags.Tag.Track = draft.trackNumber;
        tags.Tag.TrackCount = draft.trackCount;
        tags.Tag.Disc = draft.discNumber;
        tags.Tag.DiscCount = draft.discCount;
        tags.Tag.Comment = string.IsNullOrWhiteSpace(tags.Tag.Comment) ? links : $"{tags.Tag.Comment}\n{links}";

        tags.Save();
    }
    catch (Exception ex) {
        return Results.Problem(ex.ToString(), null, 500, "Error writing metadata.");
    }

    try {
        File.Move(filePath, Path.Join(Paths.music, file.filename), true);
    }
    catch (Exception ex) {
        return Results.Problem(ex.ToString(), null, 500, "Error moving file.");
    }

    try {
        await Draft.CancelAndDeleteAsync(dir);
    }
    catch (Exception ex) {
        return Results.Problem(ex.ToString(), null, 500, "Failed to delete draft.");
    }

    return Results.NoContent();
}).WithOpenApi();

draftGroup.MapDelete("/{draftId}", async (HttpContext ctx, uint draftId) => {
    if (!TryAuthorize(ctx))
        return Results.Unauthorized();
    string dir = Path.Join(Paths.drafts, draftId.ToString());
    if (!Directory.Exists(dir))
        return Results.NotFound("Draft does not exist.");

    try {
        await Draft.CancelAndDeleteAsync(dir);
    }
    catch (Exception ex) {
        return Results.Problem(ex.ToString(), null, 500, "Failed to delete draft directory.");
    }

    return Results.NoContent();
}).WithOpenApi();

app.Run();

return;

static bool TryAuthorize(HttpContext ctx) {
    string[]? auth = ctx.Request.Headers.Authorization.FirstOrDefault()?.Split(' ');
    return auth is ["Bearer", { } token] && Paths.tokens.Contains(token);
}
