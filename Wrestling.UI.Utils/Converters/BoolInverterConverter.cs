using System;
using System.Windows.Data;
using System.Globalization;

namespace Wrestling.UI.Utils.Converters
{
    [ValueConversion(typeof(bool), typeof(bool))]
    public class BoolInverterConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            Convert(value);

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            Convert(value);

        private object Convert(object value) => !(value as bool?) ?? value;
    }
}