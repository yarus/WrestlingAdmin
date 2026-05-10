using System;
using System.Globalization;
using System.Windows.Data;
using Wrestling.Entities;

namespace Wrestling.UI.Material.Utils.Converters
{
    // Renders the protocol log column on MatchResultsView. Takes the typed
    // MatchAction fields (Type, IsForRed, Points) and delegates to the shared
    // MatchActionDescriber so display text stays in lockstep with whatever
    // the adapter writes into the .wrt's legacy Text field.
    public class MatchActionDescriptionConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 3) return string.Empty;

            var type = values[0] is MatchActionType t ? t : MatchActionType.Unknown;
            var isForRed = values[1] as bool?;
            var points = values[2] is int p ? p : 0;

            return MatchActionDescriber.Describe(type, isForRed, points);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
