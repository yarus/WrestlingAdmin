using System;
using System.Windows.Data;
using System.Windows.Media;

namespace Wrestling.UI.Utils.Converters
{
    public class HighlightSelectedItemConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (values.Length != 2) return Brushes.Transparent;

            Color selectedColor = Colors.CornflowerBlue;

            if (parameter != null)
            {
                var convertFromString = ColorConverter.ConvertFromString(parameter.ToString());
                if (convertFromString != null)
                {
                    selectedColor = (Color) convertFromString;
                }
            }

            if (values[0] != null && values[1] != null && values[0] == values[1])
            {
                return new SolidColorBrush(selectedColor);
            }

            return Brushes.DarkGray;
        }
        public object[] ConvertBack(object value, Type[] targetTypes,
            object parameter, System.Globalization.CultureInfo culture)
        {
            return null;
        }
    }
}