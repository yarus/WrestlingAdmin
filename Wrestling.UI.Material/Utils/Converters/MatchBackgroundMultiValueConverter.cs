using System;
using System.Windows.Data;
using System.Windows.Media;

namespace Wrestling.UI.Material.Utils.Converters
{
    public class MatchBackgroundMultiValueConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType,
            object parameter, System.Globalization.CultureInfo culture)
        {
            var property1 = values[0] as Entities.WrestlingMatch;
            var property2 = values[1] as Entities.WrestlingMatch;

            if (property1 != null && property2 != null && property1 == property2)
            {
                return Brushes.CornflowerBlue;
            }

            return Brushes.Transparent;
        }
        public object[] ConvertBack(object value, Type[] targetTypes,
            object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
