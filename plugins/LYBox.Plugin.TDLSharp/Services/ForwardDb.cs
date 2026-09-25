using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using TdLib;

namespace LYBox.Plugin.TDLSharp.Services;

/// <summary>
/// 转发去重记录。复合主键 (SourceChatId, MessageId) 保证同一源消息只记录一次最新状态。
/// </summary>
[Table("ForwardRecords")]
public class ForwardRecord
{
    public long MessageId { get; set; }
    public long NewMessageId { get; set; }
    public long SourceChatId { get; set; }
    public long TargetChatId { get; set; }
    public long MediaAlbumId { get; set; }
    public string? SourceUrl { get; set; }
    public string? TargetUrl { get; set; }
    public bool IsSuccess { get; set; }
    public DateTime ForwardedAt { get; set; }
    public string? ExtraData { get; set; }

    public static string BuildExtraData(TdApi.Message message)
    {
        return System.Text.Json.JsonSerializer.Serialize(message, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });
    }
}

/// <summary>
/// 转发去重记录库（每个 source chat 一个 db 文件）的 EF Core 上下文。
/// 首次访问时调用 <see cref="EnsureSchemaInitializedAsync"/> 由 EnsureCreated 建表（与原 linq2db 迁移 DDL 保持等价结构）。
/// </summary>
public sealed class ForwardDbContext : DbContext
{
    private static readonly SemaphoreSlim _initLock = new(1, 1);
    private static readonly HashSet<string> _initialized = new(StringComparer.OrdinalIgnoreCase);

    private readonly string _dbPath;

    private ForwardDbContext(string dbPath)
    {
        _dbPath = dbPath;
    }

    public DbSet<ForwardRecord> ForwardRecords => Set<ForwardRecord>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite($"Data Source={_dbPath}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ForwardRecord>(b =>
        {
            b.HasKey(r => new { r.SourceChatId, r.MessageId });
            b.HasIndex(r => r.NewMessageId).HasDatabaseName("IX_ForwardRecords_NewMessageId");
            b.HasIndex(r => r.MediaAlbumId).HasDatabaseName("IX_ForwardRecords_MediaAlbumId");
            b.HasIndex(r => new { r.SourceChatId, r.TargetChatId }).HasDatabaseName("IX_ForwardRecords_SourceChatId_TargetChatId");
        });
    }

    /// <summary>仅在该 db path 第一次被访问时执行 EnsureCreated 建表；后续访问跳过。</summary>
    public async Task EnsureSchemaInitializedAsync()
    {
        if (_initialized.Contains(_dbPath)) return;
        await _initLock.WaitAsync();
        try
        {
            if (_initialized.Contains(_dbPath)) return;
            await Database.EnsureCreatedAsync();
            _initialized.Add(_dbPath);
        }
        finally { _initLock.Release(); }
    }

    /// <summary>根据源 chat ID 创建连接。</summary>
    public static ForwardDbContext CreateForChat(long chatId)
    {
        Directory.CreateDirectory(TdlPaths.ForwardDbDir);
        var newPath = Path.Combine(TdlPaths.ForwardDbDir, $"forward-{chatId}.db");
        return new ForwardDbContext(newPath);
    }

    /// <summary>根据已知的 db 文件路径创建连接（用于按文件遍历清理等场景）。</summary>
    public static ForwardDbContext OpenFromPath(string dbPath)
    {
        return new ForwardDbContext(dbPath);
    }

    /// <summary>
    /// 按条件删除 ForwardRecords 中的记录。所有过滤参数都为 0 / null 时表示不过滤。
    /// 返回实际删除的记录数。
    /// </summary>
    public async Task<int> DeleteForwardRecordsAsync(
        long sourceChatId = 0,
        long targetChatId = 0,
        bool? onlySuccess = null,
        long fromMessageId = 0)
    {
        var query = ForwardRecords.AsQueryable();
        if (sourceChatId > 0) query = query.Where(r => r.SourceChatId == sourceChatId);
        if (targetChatId > 0) query = query.Where(r => r.TargetChatId == targetChatId);
        if (onlySuccess.HasValue) query = query.Where(r => r.IsSuccess == onlySuccess.Value);
        if (fromMessageId > 0) query = query.Where(r => r.MessageId >= fromMessageId);

        return await query.ExecuteDeleteAsync();
    }
}