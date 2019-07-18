using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;

namespace Wrestling.UI.Utils.Converters
{
    public class FullPathToFileNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var str = value as string;

            return string.IsNullOrEmpty(str) ? string.Empty : Path.GetFileName(str);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }
    }
}
