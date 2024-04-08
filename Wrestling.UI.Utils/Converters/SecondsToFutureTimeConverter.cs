using System;
using System.Globalization;
using System.Windows.Data;


namespace Wrestling.UI.Utils.Converters
{
    public class SecondsToFutureTimeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return DateTime.Now.ToString(@"HH\:mm");

            var finishDateTime = DateTime.Now.AddSeconds((int)value);

            if (finishDateTime.Date != DateTime.Now.Date)
            {
                return finishDateTime.ToString(@"dd\.HH\:mm");
            }

            return finishDateTime.ToString(@"HH\:mm");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return parameter;
        }
    }
}
