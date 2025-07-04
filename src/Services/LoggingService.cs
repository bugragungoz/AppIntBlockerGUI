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
        private readonly object _eventLock = new object();
        private event Action<string>? _logEntryAdded;

        public event Action<string> LogEntryAdded
        {
            add
            {
                lock (_eventLock)
                {
                    _logEntryAdded += value;
                }
            }
            remove
            {
                lock (_eventLock)
                {
                    _logEntryAdded -= value;
                }
            }
        }

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

        private void InvokeLogEntryAdded(string logEntry)
        {
            Action<string>? handler;
            lock (_eventLock)
            {
                handler = _logEntryAdded;
            }
            
            // Invoke on UI thread if needed
            if (System.Windows.Application.Current?.Dispatcher.CheckAccess() == true)
            {
                handler?.Invoke(logEntry);
            }
            else
            {
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(() => handler?.Invoke(logEntry));
            }
        }

        public void LogInfo(string message)
        {
            var logEntry = $"[{DateTime.Now:HH:mm:ss}] INFO: {message}";
            _logger.Information(message);
            InvokeLogEntryAdded(logEntry);
        }

        public void LogWarning(string message)
        {
            var logEntry = $"[{DateTime.Now:HH:mm:ss}] WARN: {message}";
            _logger.Warning(message);
            InvokeLogEntryAdded(logEntry);
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
            InvokeLogEntryAdded(logEntry);
        }

        public void LogDebug(string message)
        {
            var logEntry = $"[{DateTime.Now:HH:mm:ss}] DEBUG: {message}";
            _logger.Debug(message);
            InvokeLogEntryAdded(logEntry);
        }
    }
} 