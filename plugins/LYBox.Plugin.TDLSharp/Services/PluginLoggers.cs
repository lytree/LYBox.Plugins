using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace LYBox.Plugin.TDLSharp.Services;

/// <summary>
/// 统一日志入口：所有插件都通过本类创建 <see cref="ILogger"/>，避免散落的 <c>Console.WriteLine</c>。
///
/// 解析顺序：
/// <list type="number">
/// <item>优先通过 <see cref="TdlPaths.LoggerFactory"/>（即 <see cref="IPluginHostEnvironment.LoggerFactory"/>）创建。</item>
/// <item>若 <see cref="TdlPaths"/> 未初始化（<see cref="TdlPaths.IsInitialized"/> = false，
///       例如老库迁移路径在宿主初始化前被触发），回退到 <see cref="NullLoggerFactory"/>，
///       静默丢弃日志（不写 Console / 不写文件），保持原"被 catch 吞掉"的语义。</item>
/// </list>
///
/// 使用建议：所有 catch / 诊断块统一调
/// <code>
/// PluginLoggers.For&lt;TMyClass&gt;().LogWarning(ex, "失败原因: {Message}", ex.Message);
/// </code>
/// </summary>
public static class PluginLoggers
{
    /// <summary>按调用方类型创建 logger（category = typeof(T).FullName）。</summary>
    public static ILogger<T> For<T>()
    {
        if (TdlPaths.IsInitialized)
        {
            return TdlPaths.LoggerFactory.CreateLogger<T>();
        }
        return NullLogger<T>.Instance;
    }

    /// <summary>按字符串 category 创建 logger（用于静态类等无类型上下文场景）。</summary>
    public static ILogger For(string categoryName)
    {
        if (TdlPaths.IsInitialized)
        {
            return TdlPaths.LoggerFactory.CreateLogger(categoryName);
        }
        return NullLogger.Instance;
    }
}