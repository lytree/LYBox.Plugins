using System.Text;
using LYBox.Plugin.Downloader.Utils;
using LYBox.Plugin.Shared.Services;

namespace LYBox.Plugin.Downloader.Config;

/// <summary>
/// 一次性数据迁移：把历史独立插件 <c>LYBox.Plugin.DouyinDownloader</c> 的数据目录
/// <c>Data/DouyinDownloader/</c>（settings.json / cookies.json / dy_downloader.db）
/// 并入本插件数据目录下的 <c>douyin/</c> 子目录（<c>Data/{PluginId}/douyin/</c>），
/// 迁移成功后清理旧目录。
///
/// 设计要点：
/// <list type="bullet">
/// <item>只跑一次：目标目录写入 <c>.migrated-from-DouyinDownloader</c> 标记后不再尝试；
///       标记内含迁移清单与清理结果，作为磁盘上的审计记录（插件日志出口默认为空，不能只靠日志）。</item>
/// <item>先记录后删除：标记先落盘，再删除旧文件 —— 标记写失败就一个文件都不删，下次启动重试。</item>
/// <item>只删已迁走的：被跳过的文件（目标已有同名文件）留在旧目录，此时整个旧目录予以保留，
///       避免把可能是唯一副本的数据删掉。</item>
/// <item>不阻塞：任何失败只记日志，绝不影响插件加载。</item>
/// </list>
/// </summary>
public static class PluginDataMigration
{
    /// <summary>迁移完成标记文件名（位于新的 douyin 数据目录内，同时充当审计记录）。</summary>
    private const string MarkerFileName = ".migrated-from-DouyinDownloader";

    /// <summary>SQLite WAL 边车文件后缀。</summary>
    private const string WalSuffix = "-wal";

    /// <summary>SQLite 共享内存边车文件后缀。</summary>
    private const string ShmSuffix = "-shm";

    /// <summary>
    /// 执行迁移。<paramref name="provider"/> 为 <c>null</c>（宿主未提供数据目录契约）时静默跳过。
    /// 必须在任何组件读取数据目录（settings.json / cookies.json / *.db）之前调用。
    /// </summary>
    public static void MigrateLegacyDouyinData(IPluginDataDirectoryProvider? provider)
    {
        if (provider is null) return;

        var legacyDir = PluginConfigStore.ResolveLegacyDouyinDir(provider);

        // 防呆：清理是递归删除，路径不符合预期就整体放弃迁移（此检查只读不写）
        if (!IsSafeToPurge(legacyDir))
        {
            Logger.Warn($"[数据迁移] 旧目录路径不符合预期，已跳过迁移以策安全: {legacyDir}");
            return;
        }

        try
        {
            var targetDir = PluginConfigStore.ResolveDouyinDir(provider);
            var markerPath = Path.Combine(targetDir, MarkerFileName);
            if (File.Exists(markerPath)) return;   // 已迁移过

            Directory.CreateDirectory(targetDir);

            var copied = new List<string>();
            var kept = new List<string>();
            if (Directory.Exists(legacyDir))
                CopyMissing(legacyDir, targetDir, copied, kept);

            // 先落标记：即使紧接着清理失败，也不会出现"删了数据却没记录"的状态
            File.WriteAllText(markerPath, BuildReport(legacyDir, copied, kept, purgeNote: "pending"));

            // 清理旧目录（只删本次复制走的文件；仍有保留文件时整个目录不删）
            var purgeNote = PurgeLegacy(legacyDir, copied);

            // 把清理结果补记进标记
            try { File.AppendAllText(markerPath, $"purge: {purgeNote}{Environment.NewLine}"); } catch { /* 补记失败不影响迁移结果 */ }

            if (copied.Count > 0)
                Logger.Info($"[数据迁移] 抖音数据已并入新目录：{legacyDir} → {targetDir}（{copied.Count} 个文件）");
            if (kept.Count > 0)
                Logger.Warn($"[数据迁移] 旧目录保留 {kept.Count} 个未迁移文件（目标已存在同名文件），未自动清理: {legacyDir}");
        }
        catch (Exception ex)
        {
            // 标记未写入 → 下次启动自动重试
            Logger.Warn($"[数据迁移] 抖音数据目录迁移失败，将在下次启动重试: {ex.Message}");
        }
    }

    /// <summary>递归复制 <paramref name="srcDir"/> 中目标端不存在的文件；记录已复制与被跳过的文件。</summary>
    private static void CopyMissing(string srcDir, string dstDir, List<string> copied, List<string> kept)
    {
        Directory.CreateDirectory(dstDir);
        var copiedHere = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 第一轮：普通文件（跳过 SQLite 边车）
        foreach (var file in Directory.EnumerateFiles(srcDir))
        {
            var name = Path.GetFileName(file);
            if (IsSqliteSidecar(name)) continue;

            var dst = Path.Combine(dstDir, name);
            if (File.Exists(dst)) { kept.Add(file); continue; }

            File.Copy(file, dst, overwrite: false);
            copiedHere.Add(name);
            copied.Add(file);
        }

        // 第二轮：边车文件必须与主库同批复制。若目标是「新主库 + 旧 WAL」的错配组合，
        // SQLite 会读到不一致的页 —— 宁可丢掉未 checkpoint 的事务，也不制造损坏的库。
        foreach (var file in Directory.EnumerateFiles(srcDir))
        {
            var name = Path.GetFileName(file);
            if (!IsSqliteSidecar(name)) continue;

            var dst = Path.Combine(dstDir, name);
            if (File.Exists(dst)) { kept.Add(file); continue; }
            if (!copiedHere.Contains(name[..^4])) { kept.Add(file); continue; }   // 主库本批未复制 → 不贴边车

            File.Copy(file, dst, overwrite: false);
            copied.Add(file);
        }

        foreach (var dir in Directory.EnumerateDirectories(srcDir))
            CopyMissing(Path.Combine(srcDir, Path.GetFileName(dir)), Path.Combine(dstDir, Path.GetFileName(dir)), copied, kept);
    }

    /// <summary>
    /// 删除本次已复制走的旧文件，并在旧目录已空时移除整个目录（含残留空子目录）。
    /// 只要还剩文件（被跳过的 / 删除失败的），整个旧目录原样保留。返回给审计记录的一句话结论。
    /// </summary>
    private static string PurgeLegacy(string legacyDir, List<string> copied)
    {
        if (!Directory.Exists(legacyDir)) return "legacy dir absent";

        foreach (var src in copied)
        {
            try { File.Delete(src); } catch { /* 删除失败 → 该文件会出现在下方 remaining 里，目录随之保留 */ }
        }

        var remaining = Directory.EnumerateFiles(legacyDir, "*", SearchOption.AllDirectories).Count();
        if (remaining > 0)
            return $"legacy dir kept ({remaining} file(s) left)";

        try
        {
            Directory.Delete(legacyDir, recursive: true);
            return "legacy dir removed";
        }
        catch (Exception ex)
        {
            return $"legacy dir removal failed: {ex.Message}";
        }
    }

    /// <summary>
    /// 防呆校验：只允许清理形如 <c>{HostDataRoot}/{LegacyDouyinSubId}</c> 的这一层目录，
    /// 且其父目录不能是盘根（例如 <c>D:\DouyinDownloader</c>）—— 递归删除不允许有这类歧义。
    /// </summary>
    private static bool IsSafeToPurge(string legacyDir)
    {
        if (string.IsNullOrWhiteSpace(legacyDir)) return false;

        var normalized = Path.TrimEndingDirectorySeparator(Path.GetFullPath(legacyDir));
        if (!string.Equals(Path.GetFileName(normalized), PluginConfigStore.LegacyDouyinSubId, StringComparison.Ordinal))
            return false;

        var parent = Path.GetDirectoryName(normalized);
        if (string.IsNullOrEmpty(parent)) return false;

        var root = Path.GetPathRoot(normalized);
        return !string.Equals(Path.TrimEndingDirectorySeparator(parent),
                               Path.TrimEndingDirectorySeparator(root ?? string.Empty),
                               StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildReport(string legacyDir, List<string> copied, List<string> kept, string purgeNote)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"migrated from: {legacyDir}");
        sb.AppendLine($"at: {DateTimeOffset.Now:O}");
        sb.AppendLine($"copied: {copied.Count}");
        sb.AppendLine($"kept: {kept.Count}{(kept.Count == 0 ? "" : " (" + string.Join(", ", kept.Select(Path.GetFileName)) + ")")}");
        sb.AppendLine($"purge: {purgeNote}");
        return sb.ToString();
    }

    private static bool IsSqliteSidecar(string fileName)
        => fileName.EndsWith(WalSuffix, StringComparison.OrdinalIgnoreCase)
           || fileName.EndsWith(ShmSuffix, StringComparison.OrdinalIgnoreCase);
}
