// <copyright file="ILoggingService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace AppIntBlockerGUI.Services
{
    using System;

    public interface ILoggingService
    {
        void LogInfo(string message);

        void LogWarning(string message);

        void LogError(string message, Exception? exception = null);

        void LogDebug(string message);

        event Action<string> LogEntryAdded;
    }
} 