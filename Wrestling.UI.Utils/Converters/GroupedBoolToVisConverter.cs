using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Wrestling.UI.Utils.Converters
{
    [ValueConversion(typeof(bool), typeof(Visibility))]
    public class GroupedBoolToVisConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is bool)) return Visibility.Hidden;

            return (bool) value ? Visibility.Visible : Visibility.Hidden;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value as Visibility? == Visibility.Visible;
        }
    }
}
