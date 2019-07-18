using System;
using System.Globalization;
using System.Windows.Data;

namespace Wrestling.UI.Utils.Converters
{
    public class SecondsToTimeSpanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return new TimeSpan(0, 0, 0, 0);

            return new TimeSpan(0, 0, 0, (int)value);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return parameter;
        }
    }
}
