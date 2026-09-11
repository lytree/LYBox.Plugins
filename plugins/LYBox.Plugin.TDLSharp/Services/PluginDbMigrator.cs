using Microsoft.Data.Sqlite;

namespace LYBox.Plugin.TDLSharp.Services;

/// <summary>
/// 一条数据库迁移步骤。<see cref="Version"/> 单调递增，SQL 语句按顺序执行。
/// 初始建表、加字段（ALTER TABLE ... ADD COLUMN）、建索引等都可以是一步。
/// </summary>
/// <param name="Version">schema 版本号（从 1 开始，每个库文件独立记录）</param>
/// <param name="Description">人读描述（写入日志）</param>
/// <param name="Statements">按顺序执行的 SQL 语句</param>
public sealed record DbMigrationStep(int Version, string Description, IReadOnlyList<string> Statements);

/// <summary>
/// 插件本地 SQLite 数据库的版本化迁移器（仅依赖 Microsoft.Data.Sqlite，与 ORM 无关）。
///
/// 机制：每个 db 文件用 SQLite 自带的 <c>PRAGMA user_version</c> 记录当前 schema 版本。
/// 首次访问该文件时对比目标版本，只执行缺失的步骤：
/// <list type="bullet">
/// <item>初始安装（文件不存在/空库）：从第 1 步全量执行（建表、索引）；</item>
/// <item>历史遗留库（旧版 EnsureCreated 创建，user_version == 0 但主表已存在）：
///       自动认作基线版本，跳过建表步骤，只执行基线之后的变更；</item>
/// <item>插件升级引入新步骤（如加字段）：只执行未应用的步骤，每个步骤独立事务，
///       失败整体回滚、下次访问自动重试。</item>
/// </list>
/// </summary>
public static class PluginDbMigrator
{
    /// <summary>迁移日志回调（可选）。未设置时退化为 Debug.WriteLine。</summary>
    public static Action<string>? Log { get; set; }

    private static void WriteLog(string message)
    {
        if (Log != null) Log(message);
        else System.Diagnostics.Debug.WriteLine($"[PluginDbMigrator] {message}");
    }

    /// <summary>
    /// 将 <paramref name="dbPath"/> 迁移到 <paramref name="steps"/> 的最高版本。
    /// 幂等：已到位时只做一次 PRAGMA 读取即返回。
    /// </summary>
    /// <param name="dbPath">数据库文件完整路径</param>
    /// <param name="mainTableName">主表名，用于识别"EnsureCreated 时代"的遗留库</param>
    /// <param name="baselineVersion">基线版本号 = 初始建表步骤的 Version</param>
    /// <param name="steps">全部迁移步骤（含建表步骤）</param>
    public static async Task MigrateAsync(
        string dbPath,
        string mainTableName,
        int baselineVersion,
        IReadOnlyList<DbMigrationStep> steps,
        CancellationToken ct = default)
    {
        var dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var target = steps.Count == 0 ? 0 : steps.Max(s => s.Version);

        await using var conn = new SqliteConnection($"Data Source={dbPath}");
        await conn.OpenAsync(ct);

        var current = await ReadUserVersionAsync(conn, ct);

        var mainTableExists = await TableExistsAsync(conn, mainTableName, ct);
        if (current == 0 && mainTableExists)
        {
            // EnsureCreated / 旧版插件创建的库：结构视为基线版本，后续步骤照常增量执行
            current = baselineVersion;
            await WriteUserVersionAsync(conn, baselineVersion, ct);
            WriteLog($"已存在但无版本标记，按基线 v{baselineVersion} 处理: {Path.GetFileName(dbPath)}");
        }
        else if (!mainTableExists && current < baselineVersion)
        {
            // 空库或异常状态：从零全量执行
            current = 0;
        }

        if (current >= target) return;

        foreach (var step in steps.Where(s => s.Version > current).OrderBy(s => s.Version))
        {
            await using var tx = (SqliteTransaction)await conn.BeginTransactionAsync(ct);
            try
            {
                foreach (var sql in step.Statements)
                {
                    await using var cmd = conn.CreateCommand();
                    cmd.Transaction = tx;
                    cmd.CommandText = sql;
                    await cmd.ExecuteNonQueryAsync(ct);
                }

                // user_version 写在头页，与步骤同一事务：步骤失败则版本号一并回滚
                await using var verCmd = conn.CreateCommand();
                verCmd.Transaction = tx;
                verCmd.CommandText = $"PRAGMA user_version = {step.Version};";
                await verCmd.ExecuteNonQueryAsync(ct);

                await tx.CommitAsync(ct);
                WriteLog($"已应用迁移 v{step.Version}（{step.Description}）: {Path.GetFileName(dbPath)}");
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(ct);
                WriteLog($"迁移 v{step.Version} 失败，已回滚: {Path.GetFileName(dbPath)} - {ex.Message}");
                throw;
            }
        }
    }

    private static async Task<long> ReadUserVersionAsync(SqliteConnection conn, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA user_version;";
        var result = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt64(result);
    }

    private static async Task WriteUserVersionAsync(SqliteConnection conn, int version, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA user_version = {version};";
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task<bool> TableExistsAsync(SqliteConnection conn, string tableName, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name;";
        cmd.Parameters.AddWithValue("$name", tableName);
        var result = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt64(result) > 0;
    }
}
