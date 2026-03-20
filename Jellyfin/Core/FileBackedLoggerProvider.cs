using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Windows.Storage;
using Windows.System.Profile;

namespace Jellyfin.Core;

internal sealed class FileBackedLoggerProvider : ILoggerProvider
{
    private readonly string _localLogFilePath;
    private Channel<string> _logPipe;

    public FileBackedLoggerProvider()
    {
        _localLogFilePath = $"jellyfin-for-xbox-{DateTime.Now:yyyy-MM-dd-HH-mm-ss}";
        _logPipe = Channel.CreateUnbounded<string>(
            new UnboundedChannelOptions
            {
                SingleWriter = false,
                SingleReader = false,
                AllowSynchronousContinuations = true
            });
        Task.Run(async () =>
        {
            await CleanupOldLogfiles();
            await CreateLogfile().ConfigureAwait(false);

            await foreach (var item in _logPipe.Reader.ReadAllAsync())
            {
                var logEntry = Encoding.UTF8.GetBytes(item);
                await LogStream.WriteAsync(logEntry, 0, logEntry.Length).ConfigureAwait(false);
            }
        });
    }

    private Stream LogStream { get; set; }

    internal async Task<IEnumerable<StorageFile>> GetLogfiles()
    {
        var logs = await Windows.Storage.ApplicationData.Current.TemporaryFolder.CreateFolderAsync("logs", Windows.Storage.CreationCollisionOption.OpenIfExists);
        var files = await logs.CreateFileQuery().GetFilesAsync();
        return files.Where(e => e.DisplayName.StartsWith("jellyfin-for-xbox-")).ToArray();
    }

    private async Task CreateLogfile()
    {
        var logs = await Windows.Storage.ApplicationData.Current.TemporaryFolder.CreateFolderAsync("logs", Windows.Storage.CreationCollisionOption.OpenIfExists);

        LogStream = await logs.OpenStreamForWriteAsync(_localLogFilePath, Windows.Storage.CreationCollisionOption.OpenIfExists).ConfigureAwait(true);

        var logBuilder = new StringBuilder();
        logBuilder.AppendLine($"Jellyfin for Xbox Client version {Assembly.GetCallingAssembly().GetName().Version}");
        logBuilder.AppendLine($"UWP version: {AnalyticsInfo.VersionInfo.DeviceFamily} {AnalyticsInfo.VersionInfo.DeviceFamilyVersion}");
        logBuilder.AppendLine($"Device info: {AnalyticsInfo.DeviceForm}");

        foreach (var deviceInfo in await AnalyticsInfo.GetSystemPropertiesAsync([
                         "App",
                         "AppVer",
                         "DeviceFamily",
                         "FlightRing",
                         "OSVersionFull",
                     ]))
        {
            logBuilder.AppendLine($"{deviceInfo.Key}: {deviceInfo.Value}");
        }

        await _logPipe.Writer.WriteAsync(logBuilder.ToString()).ConfigureAwait(false);
    }

    public async Task<Stream> ReadLogfile(string logfileName)
    {
        var logs = await Windows.Storage.ApplicationData.Current.TemporaryFolder.CreateFolderAsync("logs", Windows.Storage.CreationCollisionOption.OpenIfExists);
        if (logfileName == _localLogFilePath)
        {
            LogStream.Flush();
        }

        return await logs.OpenStreamForReadAsync(logfileName).ConfigureAwait(true);
    }

    private async Task CleanupOldLogfiles()
    {
        var logs = await Windows.Storage.ApplicationData.Current.TemporaryFolder.CreateFolderAsync("logs", Windows.Storage.CreationCollisionOption.OpenIfExists);
        var logFiles = (await logs.GetFilesAsync())
            .OrderByDescending(f => f.DateCreated)
            .ToList();
        var filesToDelete = logFiles.Skip(5);
        foreach (var file in filesToDelete)
        {
            await file.DeleteAsync();
        }
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new UwpAppLogger(this);
    }

    public void Dispose()
    {
        LogStream.Flush();
        LogStream.Dispose();
    }

    internal class UwpAppLogger : ILogger
    {
        private readonly FileBackedLoggerProvider _appLoggerProvider;

        public UwpAppLogger(FileBackedLoggerProvider appLoggerProvider)
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
            var logEntry = $"{DateTime.Now:s} [{logLevel}] {message}\n";

            _appLoggerProvider._logPipe.Writer.TryWrite(logEntry);
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
        {
            throw new NotImplementedException();
        }
    }
}
