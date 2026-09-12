namespace LYBox.Plugin.DouyinDownloader.Utils;

/// <summary>简单日志门面（注入式,默认吞掉,可在宿主里挂载 Sink）。</summary>
public static class Logger
{
    public static Action<string>? Sink;
    public static void Info(string s) => Sink?.Invoke(s);
    public static void Warn(string s) => Sink?.Invoke("[WARN] " + s);
    public static void Error(string s) => Sink?.Invoke("[ERROR] " + s);
}
