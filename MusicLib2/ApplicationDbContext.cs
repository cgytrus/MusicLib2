using System.Collections.Immutable;
using Microsoft.EntityFrameworkCore;

namespace MusicLib2;

public class ApplicationDbContext : DbContext {
    public static readonly string baseDir = Environment.GetEnvironmentVariable("ML2_PATH") ??
        Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MusicLib2");
    public static readonly string musicDir = Environment.GetEnvironmentVariable("ML2_PATH") is null ?
        Environment.GetFolderPath(Environment.SpecialFolder.MyMusic) : Path.Join(baseDir, "music");
    public static readonly string draftDir = Path.Join(baseDir, "drafts");

    public static readonly ImmutableHashSet<string> tokens =
        File.ReadLines(Path.Join(baseDir, "auth.txt")).ToImmutableHashSet();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
        optionsBuilder.UseSqlite($"Data Source={Path.Join(baseDir, "cache.db")}");
}
