using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using TdLib;

namespace LYBox.Plugin.TDLSharp.Services;

/// <summary>
/// 转发去重记录。复合主键 (SourceChatId, MessageId) 保证同一源消息只记录一次最新状态。
/// ExecutionHistoryRecordId / ExecutionHistoryScriptId 记录"本次转发由哪个脚本的哪次执行产生"，
/// 便于执行历史被删除时联动删除对应转发记录（NULL 表示旧记录，无关联）。
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

    /// <summary>产生本次转发的 ExecutionHistoryRecord.Id（NULL = 老数据，无关联）。</summary>
    public int? ExecutionHistoryRecordId { get; set; }

    /// <summary>产生本次转发的脚本 Id（与 ExecutionHistoryRecordId 配对使用，跨 db 定位时辅助用）。</summary>
    public string? ExecutionHistoryScriptId { get; set; }

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
            // 索引用于"按执行历史记录删除其产生的全部转发记录"。
            b.HasIndex(r => new { r.ExecutionHistoryScriptId, r.ExecutionHistoryRecordId })
                .HasDatabaseName("IX_ForwardRecords_ExecutionHistory");
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

    /// <summary>
    /// 删除指定执行历史记录产生的全部转发记录。返回总删除数（跨所有 forward-*.db 文件）。
    /// </summary>
    public static async Task<int> DeleteByExecutionHistoryAsync(string scriptId, int executionHistoryRecordId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(scriptId) || executionHistoryRecordId <= 0) return 0;

        Directory.CreateDirectory(TdlPaths.ForwardDbDir);
        var dbFiles = Directory.EnumerateFiles(TdlPaths.ForwardDbDir, "forward-*.db").ToList();

        int total = 0;
        foreach (var path in dbFiles)
        {
            ct.ThrowIfCancellationRequested();
            if (!File.Exists(path)) continue;

            try
            {
                using var db = OpenFromPath(path);
                await db.EnsureSchemaInitializedAsync();
                var deleted = await db.ForwardRecords
                    .Where(r => r.ExecutionHistoryScriptId == scriptId
                             && r.ExecutionHistoryRecordId == executionHistoryRecordId)
                    .ExecuteDeleteAsync(ct);
                total += deleted;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ForwardDb] 按执行历史清理 {Path.GetFileName(path)} 失败: {ex.Message}");
            }
        }
        return total;
    }
}