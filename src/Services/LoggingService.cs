// <copyright file="LoggingService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace AppIntBlockerGUI.Services
{
    using System;
    using System.IO;
    using Serilog;

    public class LoggingService : ILoggingService
    {
        private readonly ILogger logger;
        private readonly object eventLock = new object();

        private event Action<string>? logEntryAdded;

        public event Action<string> LogEntryAdded
        {
            add
            {
                lock (this.eventLock)
                {
                    this.logEntryAdded += value;
                }
            }

            remove
            {
                lock (this.eventLock)
                {
                    this.logEntryAdded -= value;
                }
            }
        }

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
                handler = this.logEntryAdded;
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
            var logEntry = $"[{DateTime.Now:HH:mm:ss}] DEBUG: {message}";
            this.logger.Debug(message);
            this.InvokeLogEntryAdded(logEntry);
        }
    }
}
