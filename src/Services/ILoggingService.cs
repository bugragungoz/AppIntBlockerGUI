// <copyright file="ILoggingService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace AppIntBlockerGUI.Services
{
    using System;
    using System.Collections.ObjectModel;

    public interface ILoggingService
    {
        event Action<string> LogEntryAdded;

        void LogInfo(string message);

        void LogWarning(string message);

        void LogError(string message, Exception? ex = null);

        void LogDebug(string message);

        void LogCritical(string message, Exception? ex = null);

        ObservableCollection<string> GetLogs();

        void ClearLogs();
    }
} 