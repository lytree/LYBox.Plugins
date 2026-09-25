using LYBox.Plugin.Shared.Paths;
using LYBox.Plugin.Shared.Services;
using Microsoft.Extensions.Logging;

namespace LYBox.Plugin.TDLSharp.Services;

/// <summary>
/// 集中管理插件默认数据目录。所有脚本输出文件均落在主体的 <c>{HostDataRoot}/{PluginId}/</c> 之下，
/// 避免写入发布目录（只读场景）失败，并保证插件升级时数据被保留。
///
/// 路径来源（统一通过宿主暴露，不允许插件自行猜测）：
/// <list type="bullet">
/// <item><c>DataRoot</c> 等子目录：经 <see cref="IPluginHostEnvironment.HostDataRoot"/> +
///       <see cref="IPluginDataDirectoryProvider"/> 解析。</item>
/// <item><c>LogsDirectory</c>：经 <see cref="IPluginHostEnvironment.LogsDirectory"/>。</item>
/// <item><c>AppBaseDirectory</c>：经 <see cref="IPluginHostEnvironment.AppBaseDirectory"/>。</item>
/// </list>
///
/// 初始化：插件入口 <c>RegisterAsync</c> 必须调用 <see cref="Initialize"/> 注入宿主环境，
/// 否则任何路径访问都会抛 <see cref="InvalidOperationException"/>（不再静默 fallback）。
///
/// TDLib 会话数据（tdl/）的默认位置由 <c>TDLSharpPlugin.GetDefaultTdlRoot()</c> 单独管理
/// （%USERPROFILE%\.tdl），不经过本类。
/// </summary>
public static class TdlPaths
{
    /// <summary>PluginId 必须与 csproj 中的 PluginId 保持一致。</summary>
    public const string PluginId = "A1B2C3D4-E5F6-7890-ABCD-TDLSHARP00001";

    private static IPluginHostEnvironment? _hostEnv;
    private static IPluginDataDirectoryProvider? _dataDirProvider;

    /// <summary>
    /// 由插件入口（<c>RegisterAsync</c>）调用一次，注入宿主环境。
    /// 重复调用以最后一次为准；null 表示解除绑定（测试场景）。
    /// </summary>
    public static void Initialize(IPluginHostEnvironment? hostEnv, IPluginDataDirectoryProvider? dataDirProvider = null)
    {
        _hostEnv = hostEnv;
        _dataDirProvider = dataDirProvider;
    }

    /// <summary>宿主环境是否已注入。</summary>
    public static bool IsInitialized => _hostEnv is not null;

    /// <summary>
    /// 数据根目录：<c>{HostDataRoot}/{PluginId}/</c>。
    /// 若 <see cref="Initialize"/> 未调用，抛 <see cref="InvalidOperationException"/>。
    /// </summary>
    public static string DataRoot
    {
        get
        {
            var provider = RequireDataDirProvider();
            return provider.GetPluginDataDirectory(PluginId);
        }
    }

    public static string DataSubdir(string leaf) => Path.Combine(DataRoot, leaf);

    /// <summary>默认下载目录(脚本未指定输出目录时使用)。</summary>
    public static string DefaultDownloadDir => RequireDataDirProvider().GetPluginDownloadsDirectory(PluginId);

    /// <summary>默认消息导出目录(脚本未指定输出目录时使用)。</summary>
    public static string DefaultExportDir => DataSubdir(PluginSubDirectories.Exports);

    /// <summary>默认聊天列表导出目录。</summary>
    public static string DefaultChatsDir => DataSubdir(PluginSubDirectories.Exports);

    /// <summary>默认成员列表导出目录。</summary>
    public static string DefaultMembersDir => DataSubdir(PluginSubDirectories.Exports);

    /// <summary>默认转发记录数据库目录(每个 source chat 独立一个 db 文件)。</summary>
    public static string ForwardDbDir => DataSubdir(PluginSubDirectories.Data);

    /// <summary>执行历史数据库目录(每个 script 独立一个 db 文件)。</summary>
    public static string HistoryDir => RequireDataDirProvider().GetPluginHistoryDirectory(PluginId);

    /// <summary>宿主日志目录（与宿主滚动日志 <c>app-yyyy-MM-dd_NNN.log</c> 同目录）。</summary>
    public static string LogsDirectory => RequireHostEnv().LogsDirectory;

    /// <summary>
    /// 当前插件专属日志目录：<c>{LogsDirectory}/plugins/{PluginId}/</c>。
    /// 该目录下的滚动日志仅包含本插件的输出，便于按插件归档 / 排查。
    /// </summary>
    public static string PluginLogsDirectory => RequireHostEnv().PluginLogsDirectory;

    /// <summary>宿主可执行文件所在目录（启动器根目录）。</summary>
    public static string AppBaseDirectory => RequireHostEnv().AppBaseDirectory;

    /// <summary>宿主版本。</summary>
    public static string AppVersion => RequireHostEnv().AppVersion;

    /// <summary>是否便携 / CI 模式（数据根目录被环境变量覆盖时为 true）。</summary>
    public static bool IsPortableMode => RequireHostEnv().IsPortableMode;

    /// <summary>
    /// 宿主 <see cref="ILoggerFactory"/>：插件创建的所有 <see cref="ILogger"/> 一律通过它产出，
    /// 日志会同时落到宿主控制台与滚动日志文件中（统一格式 / 统一滚动策略）。
    /// 插件不再单独 new 自己的日志文件。
    /// </summary>
    public static ILoggerFactory LoggerFactory => RequireHostEnv().LoggerFactory;

    /// <summary>便利方法：按类型快速创建 <see cref="ILogger{TCategoryName}"/>。</summary>
    public static ILogger<T> CreateLogger<T>() => LoggerFactory.CreateLogger<T>();

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

    private static IPluginHostEnvironment RequireHostEnv()
        => _hostEnv ?? throw new InvalidOperationException(
            "TdlPaths 未初始化。请在插件入口 RegisterAsync 中先调用 " +
            "TdlPaths.Initialize(IPluginHostEnvironment, IPluginDataDirectoryProvider)。");

    private static IPluginDataDirectoryProvider RequireDataDirProvider()
        => _dataDirProvider ?? throw new InvalidOperationException(
            "TdlPaths 未初始化。请在插件入口 RegisterAsync 中先调用 " +
            "TdlPaths.Initialize(IPluginHostEnvironment, IPluginDataDirectoryProvider)，" +
            "并传入 IPluginDataDirectoryProvider。");
}