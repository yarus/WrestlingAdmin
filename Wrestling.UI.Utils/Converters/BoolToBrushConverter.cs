using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Wrestling.UI.Utils.Converters
{
    public class BoolToBrushConverter : IValueConverter
    {
        private readonly Color _highlightColor = Colors.Yellow;
        private readonly Color _hideColor = Colors.Transparent;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var highlightColor = _highlightColor;
            var hideColor = _hideColor;

            if (value == null) return Brushes.Transparent;

            if (parameter != null)
            {
                var convertFromString = ColorConverter.ConvertFromString(parameter.ToString());
                if (convertFromString != null)
                {
                    highlightColor = (Color)convertFromString;
                }
            }

            return new SolidColorBrush((bool)value ? highlightColor : hideColor);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return false;

            var highlightColor = _highlightColor;

            if (parameter != null)
            {
                var convertFromString = ColorConverter.ConvertFromString(parameter.ToString());
                if (convertFromString != null)
                {
                    highlightColor = (Color)convertFromString;
                }
            }

            return (Color) value == highlightColor;
        }
    }
}
