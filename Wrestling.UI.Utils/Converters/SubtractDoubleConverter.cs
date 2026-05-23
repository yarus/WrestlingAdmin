using System;
using System.Globalization;
using System.Windows.Data;

namespace Wrestling.UI.Utils.Converters
{
    // Converts a double by subtracting the converter parameter.
    // Used to make the trailing GridViewColumn fill remaining ListView width:
    //     Width="{Binding ActualWidth, ElementName=GridList, Converter={StaticResource Subtract}, ConverterParameter=820}"
    public class SubtractDoubleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return 0d;

            double input;
            if (value is double d) input = d;
            else if (!double.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out input)) return 0d;

            double offset = 0d;
            if (parameter != null)
            {
                if (parameter is double pd) offset = pd;
                else if (!double.TryParse(parameter.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out offset)) offset = 0d;
            }

            var result = input - offset;
            return result < 0d ? 0d : result;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
