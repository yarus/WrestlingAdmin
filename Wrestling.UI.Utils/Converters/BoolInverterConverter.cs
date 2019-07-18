using System;
using System.Windows.Data;

namespace Wrestling.UI.Utils.Converters
{
    [ValueConversion(typeof(bool), typeof(bool))]
    public class BoolInverterConverter : IValueConverter
    {
        #region IValueConverter Members
        
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
            Convert(value);

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
            Convert(value);

        private object Convert(object value) =>!(value as bool?) ?? value;

        #endregion
    }
}