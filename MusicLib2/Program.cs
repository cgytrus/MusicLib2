using System.Runtime.InteropServices;
using MusicLib2;
using YoutubeDLSharp;

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

baseGroup.MapGet("/auth", (HttpContext ctx) =>
    !TryAuthorize(ctx) ? Results.Text("Invalid token") : Results.Text("")).WithOpenApi();

baseGroup.MapGet("/tracks", () => Results.Json(Track.AllTracks())).WithOpenApi();

baseGroup.MapGet("/playlists", () => Results.Json(Track.AllPlaylists())).WithOpenApi();

baseGroup.MapGet("/playlist/{fileName}", (string fileName) => {
    if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        return Results.BadRequest("Filename contains invalid character.");
    string path = Path.Join(ApplicationDbContext.musicDir, fileName);
    return !File.Exists(path) ? Results.NotFound("Playlist not found.") : Results.Json(Track.AllFromPlaylist(path));
}).WithOpenApi();

RouteGroupBuilder draftGroup = baseGroup.MapGroup("/draft");
//draftGroup.MapGet("/{id}", (HttpContext ctx, uint id) => {
//    if (!TryAuthorize(ctx))
//        return Results.Unauthorized();
//
//}).WithOpenApi();

app.Run();

return;

static bool TryAuthorize(HttpContext ctx) {
    string[]? auth = ctx.Request.Headers.Authorization.FirstOrDefault()?.Split(' ');
    return auth is ["Bearer", { } token] && ApplicationDbContext.tokens.Contains(token);
}
