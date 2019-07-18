using System;
using System.Globalization;
using System.Windows.Data;

namespace Wrestling.UI.Material.Utils.Converters
{
    [ValueConversion(typeof(string), typeof(string))]
    public class FullNameToLastNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (string.IsNullOrEmpty(value?.ToString())) return string.Empty;

            var splitBySpace = value.ToString().Split(' ');

            return splitBySpace.Length > 0 ? splitBySpace[0] : string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }
    }
}