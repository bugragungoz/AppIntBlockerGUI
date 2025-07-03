using Serilog;
using System;
using System.IO;

namespace AppIntBlockerGUI.Services
{
    public interface ILoggingService
    {
        void LogInfo(string message);
        void LogWarning(string message);
        void LogError(string message, Exception? exception = null);
        void LogDebug(string message);
        event Action<string> LogEntryAdded;
    }

    public class LoggingService : ILoggingService
    {
        private readonly ILogger _logger;
        public event Action<string> LogEntryAdded = delegate { };

        public LoggingService()
        {
            // Ensure Logs directory exists
            var logsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            Directory.CreateDirectory(logsDir);

            // Configure Serilog
            _logger = new LoggerConfiguration()
                .WriteTo.File(Path.Combine(logsDir, "AppIntBlocker.log"),
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            LogInfo("LoggingService initialized");
        }

        public void LogInfo(string message)
        {
            var logEntry = $"[{DateTime.Now:HH:mm:ss}] INFO: {message}";
            _logger.Information(message);
            LogEntryAdded?.Invoke(logEntry);
        }

        public void LogWarning(string message)
        {
            var logEntry = $"[{DateTime.Now:HH:mm:ss}] WARN: {message}";
            _logger.Warning(message);
            LogEntryAdded?.Invoke(logEntry);
        }

        public void LogError(string message, Exception? exception = null)
        {
            var logEntry = $"[{DateTime.Now:HH:mm:ss}] ERROR: {message}";
            if (exception != null)
            {
                logEntry += $" - {exception.Message}";
                _logger.Error(exception, message);
            }
            else
            {
                _logger.Error(message);
            }
            LogEntryAdded?.Invoke(logEntry);
        }

        public void LogDebug(string message)
        {
            var logEntry = $"[{DateTime.Now:HH:mm:ss}] DEBUG: {message}";
            _logger.Debug(message);
            LogEntryAdded?.Invoke(logEntry);
        }
    }
} 