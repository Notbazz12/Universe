using System;
using System.IO;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace NoFences.Services
{
    /// <summary>
    /// Centralized logging service using NLog
    /// </summary>
    public interface ILoggingService
    {
        void LogDebug(string message);
        void LogInfo(string message);
        void LogWarning(string message);
        void LogError(string message, Exception exception = null);
        void LogFatal(string message, Exception exception = null);
    }

    public class LoggingService : ILoggingService
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static bool _isConfigured = false;

        public LoggingService()
        {
            if (!_isConfigured)
            {
                ConfigureLogging();
                _isConfigured = true;
            }
        }

        private void ConfigureLogging()
        {
            var config = new LoggingConfiguration();

            // Console target for debugging
            var consoleTarget = new ColoredConsoleTarget("console")
            {
                Layout = @"${date:format=HH\:mm\:ss} ${level} ${message} ${exception}"
            };

            // File target for persistent logs
            var logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NoFences",
                "Logs"
            );

            Directory.CreateDirectory(logDirectory);

            var fileTarget = new FileTarget("file")
            {
                FileName = Path.Combine(logDirectory, "NoFences.log"),
                Layout = @"${longdate}|${level:uppercase=true}|${logger}|${message}${when:when=length('${exception}')>0:Inner=${newline}${exception:format=tostring}}",
                ArchiveFileName = Path.Combine(logDirectory, "NoFences.{#}.log"),
                ArchiveEvery = FileArchivePeriod.Day,
                ArchiveNumbering = ArchiveNumberingMode.Rolling,
                MaxArchiveFiles = 7,
                ConcurrentWrites = true,
                KeepFileOpen = false
            };

            // Rules
            config.AddRule(LogLevel.Debug, LogLevel.Fatal, consoleTarget);
            config.AddRule(LogLevel.Info, LogLevel.Fatal, fileTarget);

            LogManager.Configuration = config;
        }

        public void LogDebug(string message)
        {
            Logger.Debug(message);
        }

        public void LogInfo(string message)
        {
            Logger.Info(message);
        }

        public void LogWarning(string message)
        {
            Logger.Warn(message);
        }

        public void LogError(string message, Exception exception = null)
        {
            if (exception != null)
                Logger.Error(exception, message);
            else
                Logger.Error(message);
        }

        public void LogFatal(string message, Exception exception = null)
        {
            if (exception != null)
                Logger.Fatal(exception, message);
            else
                Logger.Fatal(message);
        }
    }
}
