// <copyright file="LogFormattingConverter.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace AppIntBlockerGUI.Converters
{
    using System;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.Linq;
    using System.Windows.Data;

    public class LogFormattingConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 4)
            {
                return string.Empty;
            }

            var logEntries = values[0] as ObservableCollection<string>;
            var enableDetailedLogging = values[1] as bool? ?? false;
            var asciiArt = values[2] as string ?? string.Empty;
            var welcomeMessage = values[3] as string ?? string.Empty;

            if (logEntries == null)
            {
                return string.Empty;
            }

            // Combine all log entries into a single formatted string
            var formattedEntries = logEntries.Select(entry => this.FormatLogEntry(entry)).ToArray();
            return string.Join(Environment.NewLine, formattedEntries);
        }

        private string FormatLogEntry(string entry)
        {
            if (string.IsNullOrEmpty(entry))
            {
                return entry;
            }

            // Color coding for different log levels (using terminal-style formatting)
            if (entry.Contains("[ERROR]"))
            {
                return $"\u001b[31m{entry}\u001b[0m"; // Red
            }

            if (entry.Contains("[WARNING]"))
            {
                return $"\u001b[33m{entry}\u001b[0m"; // Yellow
            }

            if (entry.Contains("[SUCCESS]"))
            {
                return $"\u001b[32m{entry}\u001b[0m"; // Green
            }

            if (entry.Contains("[INFO]"))
            {
                return $"\u001b[36m{entry}\u001b[0m"; // Cyan
            }

            return entry; // Default white
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 