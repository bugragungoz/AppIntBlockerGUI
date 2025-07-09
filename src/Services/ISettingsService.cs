// <copyright file="ISettingsService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace AppIntBlockerGUI.Services
{
    using AppIntBlockerGUI.Models;

    public interface ISettingsService
    {
        AppSettings LoadSettings();

        void SaveSettings(AppSettings settings);

        string GetSettingsFilePath();
    }
} 