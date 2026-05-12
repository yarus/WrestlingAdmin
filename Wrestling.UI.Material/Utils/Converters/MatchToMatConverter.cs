using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using Wrestling.Entities;

namespace Wrestling.UI.Material.Utils.Converters
{
    public class MatchToMatConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values[0] is WrestlingMatch match && values[1] is Entities.Tournament tournament)
            {
                var mat = tournament.Mats.FirstOrDefault(c => c.Groups.Any(g => g.ID == match.GroupID));
                if (mat != null)
                {
                    return mat.Name;
                }
            }

            return string.Empty;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
