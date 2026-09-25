using LinqToDB;
using LinqToDB.Data;
using LinqToDB.Mapping;
using LYBox.Plugin.TDLSharp.Models;

namespace LYBox.Plugin.TDLSharp.Services;

/// <summary>
/// 执行历史库（每个脚本一个 db 文件）的 linq2db 连接。
/// 表结构由 <see cref="PluginDbMigrator"/> 按版本迁移（初始建表 / 增量变更），本类只负责读写。
/// </summary>
public sealed class ExecutionHistoryDb : DataConnection
{
    private static readonly SemaphoreSlim _initLock = new(1, 1);
    private static readonly HashSet<string> _initialized = new(StringComparer.OrdinalIgnoreCase);

    private readonly string _dbPath;

    private static readonly MappingSchema Mapping = CreateMapping();

    private ExecutionHistoryDb(string dbPath) : base(BuildOptions(dbPath))
    {
        _dbPath = dbPath;
    }

    private static DataOptions BuildOptions(string dbPath) => new DataOptions()
        .UseSQLiteMicrosoft($"Data Source={dbPath}")
        .UseMappingSchema(Mapping);

    private static MappingSchema CreateMapping()
    {
        var builder = new FluentMappingBuilder();
        builder.Entity<ExecutionHistoryRecord>()
            .HasTableName("ExecutionRecords")
            .HasPrimaryKey(x => x.Id)
            .HasIdentity(x => x.Id)
            .Ignore(x => x.StatusIcon)
            .Ignore(x => x.DurationText);
        builder.Build();
        return builder.MappingSchema;
    }

    public ITable<ExecutionHistoryRecord> ExecutionRecords => this.GetTable<ExecutionHistoryRecord>();

    /// <summary>仅在该 db path 第一次被访问时执行版本化迁移（初始建表 / 增量结构变更）；后续访问跳过。</summary>
    public async Task EnsureSchemaInitializedAsync()
    {
        if (_initialized.Contains(_dbPath)) return;
        await _initLock.WaitAsync();
        try
        {
            if (_initialized.Contains(_dbPath)) return;
            await PluginDbMigrator.MigrateAsync(_dbPath, "ExecutionRecords", baselineVersion: 1, ExecutionHistorySteps.Steps);
            _initialized.Add(_dbPath);
        }
        finally { _initLock.Release(); }
    }

    /// <summary>根据脚本 ID 创建连接，每个脚本使用独立的 db 文件。</summary>
    public static ExecutionHistoryDb CreateForScript(string scriptId)
    {
        var dataDir = TdlPaths.HistoryDir;
        var dbPath = Path.Combine(dataDir, $"history-{TdlPaths.SafeFileName(scriptId)}.db");
        return new ExecutionHistoryDb(dbPath);
    }
}

/// <summary>执行历史库的迁移步骤定义。</summary>
internal static class ExecutionHistorySteps
{
    /// <summary>
    /// v1 初始建表，DDL 与既有 EF EnsureCreated 生成的结构逐字一致，
    /// 保证既有库（基线认领）与新建库（全量执行）结构相同。
    /// </summary>
    public static readonly DbMigrationStep[] Steps =
    [
        new(1, "初始建表 ExecutionRecords", [
            """
            CREATE TABLE IF NOT EXISTS "ExecutionRecords" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_ExecutionRecords" PRIMARY KEY AUTOINCREMENT,
                "ScriptId" TEXT NOT NULL,
                "ScriptName" TEXT NOT NULL,
                "ParametersJson" TEXT NOT NULL,
                "ParameterSummary" TEXT NOT NULL,
                "ExecutedAt" TEXT NOT NULL,
                "Duration" TEXT NOT NULL,
                "Status" TEXT NOT NULL,
                "ErrorMessage" TEXT NULL
            )
            """,
            """CREATE INDEX IF NOT EXISTS "IX_ExecutionRecords_ScriptId" ON "ExecutionRecords" ("ScriptId")""",
            """CREATE INDEX IF NOT EXISTS "IX_ExecutionRecords_ExecutedAt" ON "ExecutionRecords" ("ExecutedAt")""",
        ]),
        // ── 未来插件升级需要改表时，在此追加新步骤（migration 会按版本号增量应用）──
        // 示例（v2，加字段）：
        // new(2, "ExecutionRecords 增加 RetryCount 字段", [
        //     """ALTER TABLE "ExecutionRecords" ADD COLUMN "RetryCount" INTEGER NOT NULL DEFAULT 0""",
        // ]),
    ];
}
