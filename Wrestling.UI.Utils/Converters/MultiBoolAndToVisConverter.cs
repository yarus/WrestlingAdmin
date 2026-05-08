using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Wrestling.UI.Utils.Converters
{
    // Multi-value AND for boolean inputs → Visibility. Visible iff every
    // input is true. ConverterParameter is an optional comma-separated list
    // of zero-based indices to negate before ANDing — so passing "1" with
    // inputs [a, b] yields Visible when a && !b. Used in the bracket view
    // to gate the win-check icon (winner AND NOT disqualified) and the DSQ
    // icon (winner AND disqualified) off the same value pair.
    public class MultiBoolAndToVisConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length == 0) return Visibility.Collapsed;
            var mask = parameter as string ?? string.Empty;
            for (int i = 0; i < values.Length; i++)
            {
                var b = values[i] is bool bv && bv;
                if (!string.IsNullOrEmpty(mask) && IndexInMask(mask, i)) b = !b;
                if (!b) return Visibility.Collapsed;
            }
            return Visibility.Visible;
        }

        private static bool IndexInMask(string mask, int index)
        {
            foreach (var token in mask.Split(','))
            {
                if (int.TryParse(token.Trim(), out var n) && n == index) return true;
            }
            return false;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
