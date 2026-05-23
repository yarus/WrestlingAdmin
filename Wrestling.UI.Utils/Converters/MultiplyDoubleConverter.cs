using System;
using System.Globalization;
using System.Windows.Data;

namespace Wrestling.UI.Utils.Converters
{
    // Converts a double by multiplying by the converter parameter.
    // Used to bind a panel's Height proportionally to its parent's ActualWidth:
    //     Height="{Binding ActualWidth, ElementName=Parent, Converter={StaticResource Multiply}, ConverterParameter=0.45}"
    public class MultiplyDoubleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return 0d;

            double input;
            if (value is double d) input = d;
            else if (!double.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out input)) return 0d;

            double factor = 1d;
            if (parameter != null)
            {
                if (parameter is double pd) factor = pd;
                else if (!double.TryParse(parameter.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out factor)) factor = 1d;
            }

            var result = input * factor;
            return result < 0d ? 0d : result;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
