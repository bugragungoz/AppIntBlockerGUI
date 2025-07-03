using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace AppIntBlockerGUI.Converters
{
    public class EnabledToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool enabled)
            {
                return enabled ? Brushes.LightGreen : Brushes.Orange;
            }

            if (value is string enabledText)
            {
                return enabledText.Equals("True", StringComparison.OrdinalIgnoreCase) || 
                       enabledText.Equals("Enabled", StringComparison.OrdinalIgnoreCase)
                    ? Brushes.LightGreen : Brushes.Orange;
            }

            return Brushes.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 