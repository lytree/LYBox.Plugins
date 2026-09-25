using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Services;

namespace LYBox.Plugin.TDLSharp.Services;

/// <summary>
/// 集中管理插件默认数据目录。所有脚本输出文件均落在主体 <c>Data/{PluginId}/</c> 之下，
/// 避免写入发布目录（只读场景）失败，并保证插件升级时数据被保留。
///
/// 解析顺序：
/// <list type="number">
/// <item>经 <see cref="IPluginDataDirectoryProvider"/> 拿 <c>Data/{PluginId}/{leaf}</c>（推荐）；</item>
/// <item>回退到程序目录下 <c>Data/{PluginId}/{leaf}</c>（无 %APPDATA% 依赖）；</item>
/// </list>
///
/// TDLib 会话数据（tdl/）的默认位置由 <c>TDLSharpPlugin.GetDefaultTdlRoot()</c> 单独管理
/// （%USERPROFILE%\.tdl），不经过本类。
/// </summary>
public static class TdlPaths
{
    /// <summary>PluginId 必须与 csproj 中的 PluginId 保持一致。</summary>
    public const string PluginId = "A1B2C3D4-E5F6-7890-ABCD-TDLSHARP00001";

    /// <summary>程序目录下的回退数据根（不再依赖 %APPDATA%）。</summary>
    public static readonly string FallbackDataRoot = Path.Combine(
        AppContext.BaseDirectory, "Data", PluginId);

    /// <summary>解析后的数据根目录（不创建目录，仅算路径）。</summary>
    public static string DataRoot
    {
        get
        {
            if (ServiceLocator.TryGetService<IPluginDataDirectoryProvider>(out var provider)
                && provider is not null)
            {
                return provider.GetPluginDataDirectory(PluginId);
            }
            // 回退到程序目录下的 Data/{PluginId}/，避免再写入系统用户目录。
            return FallbackDataRoot;
        }
    }

    public static string DataSubdir(string leaf) => Path.Combine(DataRoot, leaf);

    /// <summary>默认下载目录（脚本未指定输出目录时使用）。</summary>
    public static string DefaultDownloadDir => DataSubdir("download");

    /// <summary>默认消息导出目录（脚本未指定输出目录时使用）。</summary>
    public static string DefaultExportDir => DataSubdir("message");

    /// <summary>默认聊天列表导出目录。</summary>
    public static string DefaultChatsDir => DataSubdir("chats");

    /// <summary>默认成员列表导出目录。</summary>
    public static string DefaultMembersDir => DataSubdir("members");

    /// <summary>默认转发记录数据库目录（每个 source chat 独立一个 db 文件）。</summary>
    public static string ForwardDbDir => DataSubdir("data");

    /// <summary>执行历史数据库目录（每个 script 独立一个 db 文件）。</summary>
    public static string HistoryDir => DataSubdir("history");

    /// <summary>将任意字符串清洗为文件系统安全的文件名前缀。</summary>
    public static string SafeFileName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "_";
        var buffer = new char[raw.Length];
        var len = 0;
        foreach (var c in raw)
        {
            buffer[len++] = char.IsLetterOrDigit(c) || c is '-' or '_' or '.' ? c : '_';
        }
        return new string(buffer, 0, len);
    }
}
