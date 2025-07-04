// <copyright file="LogHighlightConverter.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace AppIntBlockerGUI.Converters
{
    using System;
    using System.Globalization;
    using System.Windows.Data;

    // Keep the original LogHighlightConverter for backward compatibility
    public class LogHighlightConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string logEntry)
            {
                // Return color based on log level
                if (logEntry.Contains("[ERROR]"))
                {
                    return "#e74a35"; // Red
                }

                if (logEntry.Contains("[WARNING]"))
                {
                    return "#f1c232"; // Yellow
                }

                if (logEntry.Contains("[SUCCESS]"))
                {
                    return "#34a853"; // Green
                }

                if (logEntry.Contains("[INFO]"))
                {
                    return "#eebb88"; // Orange
                }
            }

            return "#f5f0eb"; // Default white
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
