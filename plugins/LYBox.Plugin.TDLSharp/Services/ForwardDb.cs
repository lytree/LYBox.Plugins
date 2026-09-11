using System.ComponentModel.DataAnnotations;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using LinqToDB;
using LinqToDB.Data;
using LinqToDB.Mapping;
using TdLib;

namespace LYBox.Plugin.TDLSharp.Services;

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

    public static string BuildExtraData(TdApi.Message message, string? error = null)
    {
        return JsonSerializer.Serialize(message, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });
    }
}

/// <summary>
/// 转发去重记录库（每个 source chat 一个 db 文件）的 linq2db 连接。
/// 表结构由 <see cref="PluginDbMigrator"/> 按版本迁移（初始建表 / 增量变更），本类只负责读写。
/// </summary>
public sealed class ForwardDb : DataConnection
{
    private static readonly SemaphoreSlim _initLock = new(1, 1);
    private static readonly HashSet<string> _initialized = new(StringComparer.OrdinalIgnoreCase);

    private readonly string _dbPath;

    private static readonly MappingSchema Mapping = CreateMapping();

    private ForwardDb(string dbPath) : base(BuildOptions(dbPath))
    {
        _dbPath = dbPath;
    }

    private static DataOptions BuildOptions(string dbPath) => new DataOptions()
        .UseSQLiteMicrosoft($"Data Source={dbPath}")
        .UseMappingSchema(Mapping);

    private static MappingSchema CreateMapping()
    {
        var builder = new FluentMappingBuilder();
        builder.Entity<ForwardRecord>()
            .HasTableName("ForwardRecords")
            .HasPrimaryKey(x => x.SourceChatId, 0)
            .HasPrimaryKey(x => x.MessageId, 1);
        builder.Build();
        return builder.MappingSchema;
    }

    public ITable<ForwardRecord> ForwardRecords => this.GetTable<ForwardRecord>();

    /// <summary>仅在该 db path 第一次被访问时执行版本化迁移（初始建表 / 增量结构变更）；后续访问跳过。</summary>
    public async Task EnsureSchemaInitializedAsync()
    {
        if (_initialized.Contains(_dbPath)) return;
        await _initLock.WaitAsync();
        try
        {
            if (_initialized.Contains(_dbPath)) return;
            await PluginDbMigrator.MigrateAsync(_dbPath, "ForwardRecords", baselineVersion: 1, ForwardSteps.Steps);
            _initialized.Add(_dbPath);
        }
        finally { _initLock.Release(); }
    }

    /// <summary>根据源 chat ID 创建连接。</summary>
    public static ForwardDb CreateForChat(long chatId)
    {
        Directory.CreateDirectory(TdlPaths.ForwardDbDir);
        return new ForwardDb(Path.Combine(TdlPaths.ForwardDbDir, $"forward-{chatId}.db"));
    }
}

/// <summary>转发记录库的迁移步骤定义。</summary>
internal static class ForwardSteps
{
    /// <summary>
    /// v1 初始建表，DDL 与既有 EF EnsureCreated 生成的结构逐字一致
    /// （复合主键 SourceChatId+MessageId 及三个索引）。
    /// </summary>
    public static readonly DbMigrationStep[] Steps =
    [
        new(1, "初始建表 ForwardRecords", [
            """
            CREATE TABLE IF NOT EXISTS "ForwardRecords" (
                "MessageId" INTEGER NOT NULL,
                "SourceChatId" INTEGER NOT NULL,
                "NewMessageId" INTEGER NOT NULL,
                "TargetChatId" INTEGER NOT NULL,
                "MediaAlbumId" INTEGER NOT NULL,
                "SourceUrl" TEXT NULL,
                "TargetUrl" TEXT NULL,
                "IsSuccess" INTEGER NOT NULL,
                "ForwardedAt" TEXT NOT NULL,
                "ExtraData" TEXT NULL,
                CONSTRAINT "PK_ForwardRecords" PRIMARY KEY ("SourceChatId", "MessageId")
            )
            """,
            """CREATE INDEX IF NOT EXISTS "IX_ForwardRecords_NewMessageId" ON "ForwardRecords" ("NewMessageId")""",
            """CREATE INDEX IF NOT EXISTS "IX_ForwardRecords_MediaAlbumId" ON "ForwardRecords" ("MediaAlbumId")""",
            """CREATE INDEX IF NOT EXISTS "IX_ForwardRecords_SourceChatId_TargetChatId" ON "ForwardRecords" ("SourceChatId", "TargetChatId")""",
        ]),
        // ── 未来插件升级需要改表时，在此追加新步骤（migration 会按版本号增量应用）──
        // 示例（v2，加字段）：
        // new(2, "ForwardRecords 增加 SourceType 字段", [
        //     """ALTER TABLE "ForwardRecords" ADD COLUMN "SourceType" TEXT NOT NULL DEFAULT ''""",
        // ]),
    ];
}
