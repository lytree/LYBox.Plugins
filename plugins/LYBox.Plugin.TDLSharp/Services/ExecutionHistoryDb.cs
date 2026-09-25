using LYBox.Plugin.TDLSharp.Models;
using Microsoft.EntityFrameworkCore;

namespace LYBox.Plugin.TDLSharp.Services;

/// <summary>
/// 执行历史库（每个脚本一个 db 文件）的 EF Core 上下文。
/// 首次访问时调用 <see cref="EnsureSchemaInitializedAsync"/> 由 EnsureCreated 建表（与原 linq2db 迁移 DDL 保持等价结构）。
/// </summary>
public sealed class ExecutionHistoryDbContext : DbContext
{
    private static readonly SemaphoreSlim _initLock = new(1, 1);
    private static readonly HashSet<string> _initialized = new(StringComparer.OrdinalIgnoreCase);

    private readonly string _dbPath;

    private ExecutionHistoryDbContext(string dbPath)
    {
        _dbPath = dbPath;
    }

    public DbSet<ExecutionHistoryRecord> ExecutionRecords => Set<ExecutionHistoryRecord>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite($"Data Source={_dbPath}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ExecutionHistoryRecord>(b =>
        {
            // 计算属性：StatusIcon / DurationText 只读，不映射到列。
            b.Ignore(nameof(ExecutionHistoryRecord.StatusIcon));
            b.Ignore(nameof(ExecutionHistoryRecord.DurationText));

            b.HasIndex(nameof(ExecutionHistoryRecord.ScriptId)).HasDatabaseName("IX_ExecutionRecords_ScriptId");
            b.HasIndex(nameof(ExecutionHistoryRecord.ExecutedAt)).HasDatabaseName("IX_ExecutionRecords_ExecutedAt");
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

    /// <summary>根据脚本 ID 创建连接，每个脚本使用独立的 db 文件。</summary>
    public static ExecutionHistoryDbContext CreateForScript(string scriptId)
    {
        var dataDir = TdlPaths.HistoryDir;
        var dbPath = Path.Combine(dataDir, $"history-{TdlPaths.SafeFileName(scriptId)}.db");
        return new ExecutionHistoryDbContext(dbPath);
    }
}