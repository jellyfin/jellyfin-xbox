using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Core;

internal sealed class RollingAppLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<LogEvent> _logs;

    public RollingAppLoggerProvider()
    {
        _logs = new();
    }

    public static int MaxQueueSize { get; } = 100;

    public IEnumerable<LogEvent> Logs
    {
        get => _logs;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new RollingAppLogger(this);
    }

    public void Dispose()
    {
    }

    internal class RollingAppLogger : ILogger
    {
        private readonly RollingAppLoggerProvider _appLoggerProvider;

        public RollingAppLogger(RollingAppLoggerProvider appLoggerProvider)
        {
            _appLoggerProvider = appLoggerProvider;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (formatter == null)
            {
                throw new ArgumentNullException(nameof(formatter));
            }

            var message = formatter(state, exception);
            var logEvent = new LogEvent(DateTime.Now, logLevel, message, exception);
            _appLoggerProvider._logs.Enqueue(logEvent);
            if (_appLoggerProvider._logs.Count > RollingAppLoggerProvider.MaxQueueSize)
            {
                _appLoggerProvider._logs.TryDequeue(out _);
            }
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            throw new NotImplementedException();
        }
    }

    public record LogEvent
    {
        public LogEvent(DateTime timestamp, LogLevel level, string message, Exception exception)
        {
            this.Timestamp = timestamp;
            this.Level = level;
            this.Message = message;
            this.Exception = exception;
        }

        public DateTime Timestamp { get; }

        public LogLevel Level { get; }

        public string Message { get; }

        public Exception? Exception { get; }
    }
}
