using System;
using System.Globalization;
using System.Windows.Data;


namespace Wrestling.UI.Utils.Converters
{
    public class SecondsToTimeStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || (int)value == 0) return new TimeSpan(0, 0, 0, 0);

            TimeSpan time = TimeSpan.FromSeconds((int)value);

            if (time.Days > 0)
            {
                return time.ToString(@"dd\.hh\:mm");
            }

            return time.ToString(@"hh\:mm");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return parameter;
        }
    }
}
