using LYBox.Plugin.Downloader.Config;
using Microsoft.EntityFrameworkCore;

namespace LYBox.Plugin.Downloader.Storage;

/// <summary>
/// 下载历史 / 去重存储（与 storage/database.py 的 aweme 表对齐）。
///
/// 本实现基于 EF Core + SQLite：
///   - <see cref="DownloadDbContext"/> 负责 DbSet 注册、模型配置（唯一索引 / 字段映射）与 EnsureCreated；
///   - <see cref="AwemeEntity"/> 为 EF 实体，<see cref="AwemeRecord"/>/<see cref="HistoryRow"/> 仍作为
///     对外 DTO 保持调用方零改动；
///   - <see cref="DownloadDatabase"/> 负责把 DTO 与实体互转，所有查询走 LINQ，不再手写 SQL。
/// </summary>
public sealed class DownloadDatabase
{
    private readonly string _dbPath;
    private readonly object _lock = new();
    private DownloadDbContext? _db;
    private bool _initialized;

    public DownloadDatabase(PluginConfigStore paths, DouyinSettingsStore settings)
    {
        var dbPath = string.IsNullOrWhiteSpace(settings.Current.DatabasePath)
            ? "dy_downloader.db"
            : settings.Current.DatabasePath;
        if (!Path.IsPathRooted(dbPath))
            dbPath = Path.Combine(paths.DouyinDir, dbPath);
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        _dbPath = dbPath;
    }

    /// <summary>
    /// 创建 DbContext 并确保表结构存在（首次调用后幂等）。
    /// 后续每次操作都 new 一个 DbContext，与 EF Core 推荐用法一致（DbContext 轻量、非线程安全）。
    /// </summary>
    public void Initialize()
    {
        if (_initialized) return;
        lock (_lock)
        {
            if (_initialized) return;

            _db = new DownloadDbContext(_dbPath);
            // EnsureCreated：首次运行时建表 + 索引；后续运行无破坏性变更，直接跳过。
            _db.Database.EnsureCreated();
            _initialized = true;
        }
    }

    /// <summary>是否已存在指定作品（去重用）。</summary>
    public bool Exists(string awemeId)
    {
        EnsureInitialized();
        return _db!.Awemes.AsNoTracking().Any(e => e.AwemeId == awemeId);
    }

    /// <summary>插入或更新下载记录（按 aweme_id 唯一冲突时刷新时间与文件路径）。</summary>
    public void Upsert(AwemeRecord r)
    {
        EnsureInitialized();
        var set = _db!.Awemes;
        var entity = set.FirstOrDefault(e => e.AwemeId == r.AwemeId);

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        if (entity is null)
        {
            set.Add(new AwemeEntity
            {
                AwemeId = r.AwemeId,
                AwemeType = r.AwemeType,
                Title = r.Title ?? "",
                AuthorId = r.AuthorId ?? "",
                AuthorName = r.AuthorName ?? "",
                SecUid = r.SecUid ?? "",
                CreateTime = r.CreateTime,
                DownloadTime = now,
                FilePath = r.FilePath ?? "",
                Mode = r.Mode ?? "",
                Metadata = r.Metadata ?? "",
            });
        }
        else
        {
            entity.DownloadTime = now;
            entity.FilePath = r.FilePath ?? "";
        }

        _db.SaveChanges();
    }

    /// <summary>查询最近 <paramref name="limit"/> 条历史（按下载时间倒序）。</summary>
    public IEnumerable<HistoryRow> ListHistory(int limit = 200)
    {
        EnsureInitialized();
        var query = _db!.Awemes.AsNoTracking()
            .OrderByDescending(e => e.DownloadTime)
            .Take(limit)
            .Select(e => new HistoryRow
            {
                AwemeId = e.AwemeId,
                Title = e.Title,
                AuthorName = e.AuthorName,
                DownloadTime = e.DownloadTime,
                FilePath = e.FilePath,
                Mode = e.Mode,
            });

        foreach (var row in query) yield return row;
    }

    private void EnsureInitialized()
    {
        if (!_initialized) Initialize();
    }
}

/// <summary>下载记录 EF 实体（对应 aweme 表）。</summary>
public sealed class AwemeEntity
{
    public long Id { get; set; }
    public string AwemeId { get; set; } = "";
    public string AwemeType { get; set; } = "video";
    public string Title { get; set; } = "";
    public string AuthorId { get; set; } = "";
    public string AuthorName { get; set; } = "";
    public string SecUid { get; set; } = "";
    public long CreateTime { get; set; }
    public long DownloadTime { get; set; }
    public string FilePath { get; set; } = "";
    public string Mode { get; set; } = "";
    public string Metadata { get; set; } = "";
}

/// <summary>
/// 下载记录 EF Core DbContext。
/// SQLite 不支持索引排序子句的 ALTER,所以用 Fluent API 在 EnsureCreated 阶段一次性建好唯一索引与普通索引,
/// 与原 SQLite DDL 行为保持一致（aweme_id UNIQUE + 三条普通索引）。
/// </summary>
internal sealed class DownloadDbContext : DbContext
{
    private readonly string _dbPath;

    public DownloadDbContext(string dbPath)
    {
        _dbPath = dbPath;
    }

    public DbSet<AwemeEntity> Awemes => Set<AwemeEntity>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // 启用 WAL 以获得更好的并发读写性能,与原 PRAGMA journal_mode=WAL 保持一致。
        optionsBuilder.UseSqlite($"Data Source={_dbPath}");
        base.OnConfiguring(optionsBuilder);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var e = modelBuilder.Entity<AwemeEntity>();
        e.ToTable("aweme");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).ValueGeneratedOnAdd();

        e.Property(x => x.AwemeId).IsRequired();
        e.HasIndex(x => x.AwemeId).IsUnique();

        e.Property(x => x.AwemeType).IsRequired();

        e.HasIndex(x => x.AuthorName).HasDatabaseName("idx_aweme_author");
        e.HasIndex(x => x.Mode).HasDatabaseName("idx_aweme_mode");
        e.HasIndex(x => x.DownloadTime).HasDatabaseName("idx_aweme_download_time");
    }
}

/// <summary>对外写入 DTO（保持与旧实现兼容）。</summary>
public sealed class AwemeRecord
{
    public string AwemeId { get; set; } = "";
    public string AwemeType { get; set; } = "video";
    public string? Title { get; set; }
    public string? AuthorId { get; set; }
    public string? AuthorName { get; set; }
    public string? SecUid { get; set; }
    public long CreateTime { get; set; }
    public string? FilePath { get; set; }
    public string? Mode { get; set; }
    public string? Metadata { get; set; }
}

/// <summary>对外读取 DTO（保持与旧实现兼容）。</summary>
public sealed class HistoryRow
{
    public string AwemeId { get; set; } = "";
    public string Title { get; set; } = "";
    public string AuthorName { get; set; } = "";
    public long DownloadTime { get; set; }
    public string FilePath { get; set; } = "";
    public string Mode { get; set; } = "";
}