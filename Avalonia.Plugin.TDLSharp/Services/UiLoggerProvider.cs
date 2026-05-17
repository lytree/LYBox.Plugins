using Microsoft.Extensions.Logging;
using LogEntry = Avalonia.Plugin.TDLSharp.Models.LogEntry;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Avalonia.Plugin.TDLSharp.Services;

public class UiLoggerProvider : ILoggerProvider
{
    private readonly Action<LogEntry> _onLog;

    public UiLoggerProvider(Action<LogEntry> onLog)
    {
        _onLog = onLog;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new UiLogger(categoryName, _onLog);
    }

    public void Dispose() { }

    private class UiLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly Action<LogEntry> _onLog;

        public UiLogger(string categoryName, Action<LogEntry> onLog)
        {
            _categoryName = categoryName;
            _onLog = onLog;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            var level = logLevel switch
            {
                LogLevel.Trace => Models.LogLevel.Trace,
                LogLevel.Debug => Models.LogLevel.Debug,
                LogLevel.Information => Models.LogLevel.Information,
                LogLevel.Warning => Models.LogLevel.Warning,
                LogLevel.Error => Models.LogLevel.Error,
                LogLevel.Critical => Models.LogLevel.Error,
                _ => Models.LogLevel.Information
            };

            var message = formatter(state, exception);
            if (exception != null)
            {
                message = $"{message} {exception.Message}";
            }

            _onLog(new LogEntry { Message = message, Level = level });
        }
    }
}
