// <copyright file="AppSettings.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace AppIntBlockerGUI.Models
{
    public class AppSettings
    {
        public string LastSelectedPath { get; set; } = string.Empty;

        public bool BlockExeFiles { get; set; } = true;

        public bool BlockDllFiles { get; set; } = false;

        public bool IncludeSubdirectories { get; set; } = true;

        public bool CreateRestorePoint { get; set; } = false;

        public bool EnableDetailedLogging { get; set; } = true;

        public string DefaultApplicationName { get; set; } = string.Empty;

        public string ExcludedKeywords { get; set; } = string.Empty;

        public string ExcludedFiles { get; set; } = string.Empty;

        public bool UseExclusions { get; set; } = false;

        public bool RememberLastSettings { get; set; } = true;
    }
}
