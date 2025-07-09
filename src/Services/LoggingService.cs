// <copyright file="LoggingService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace AppIntBlockerGUI.Services
{
    using System;
    using System.IO;
    using Serilog;
    using System.Collections.ObjectModel;

    public class LoggingService : ILoggingService
    {
        private readonly Serilog.ILogger logger;
        private readonly ObservableCollection<string> logEntries = new();
        private readonly object eventLock = new object();

        public event Action<string> LogEntryAdded = delegate { };

        public LoggingService()
        {
            // Ensure Logs directory exists
            var logsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            Directory.CreateDirectory(logsDir);

            // Configure Serilog
            this.logger = new LoggerConfiguration()
                .WriteTo.File(
                    Path.Combine(logsDir, "AppIntBlocker.log"),
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            this.LogInfo("LoggingService initialized");
        }

        private void InvokeLogEntryAdded(string logEntry)
        {
            Action<string>? handler;
            lock (this.eventLock)
            {
                handler = this.LogEntryAdded;
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
            this.logger.Information(message);
            this.InvokeLogEntryAdded(logEntry);
        }

        public void LogWarning(string message)
        {
            var logEntry = $"[{DateTime.Now:HH:mm:ss}] WARN: {message}";
            this.logger.Warning(message);
            this.InvokeLogEntryAdded(logEntry);
        }

        public void LogError(string message, Exception? exception = null)
        {
            var logEntry = $"[{DateTime.Now:HH:mm:ss}] ERROR: {message}";
            if (exception != null)
            {
                logEntry += $" - {exception.Message}";
                this.logger.Error(exception, message);
            }
            else
            {
                this.logger.Error(message);
            }

            this.InvokeLogEntryAdded(logEntry);
        }

        public void LogDebug(string message)
        {
            this.logger.Debug(message);
            // Optional: Decide if debug messages should appear in the UI log
            // this.Log($"DEBUG: {message}");
        }

        public void LogCritical(string message, Exception? ex = null)
        {
            this.logger.Fatal(ex, message);
            this.InvokeLogEntryAdded($"[{DateTime.Now:HH:mm:ss}] CRITICAL: {message}");
        }

        public ObservableCollection<string> GetLogs()
        {
            return new ObservableCollection<string>();
        }

        public void ClearLogs()
        {
            this.logEntries.Clear();
            this.LogInfo("Logs have been cleared.");
        }
    }
}
