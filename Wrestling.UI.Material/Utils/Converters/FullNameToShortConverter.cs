using System;
using System.Globalization;
using System.Text;
using System.Windows.Data;

namespace Wrestling.UI.Material.Utils.Converters
{
    // "Иванов Иван Иванович" → "Иванов И.И."
    // "Иванов Иван"           → "Иванов И."
    // "Иванов"                → "Иванов"
    // null / empty            → empty string
    //
    // Splits on whitespace, keeps the first token verbatim as the last name,
    // and reduces the remaining tokens to a concatenated string of single
    // initials each followed by a period (no spaces between initials).
    public class FullNameToShortConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var input = value as string;
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;

            var parts = input.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return string.Empty;
            if (parts.Length == 1) return parts[0];

            var sb = new StringBuilder(parts[0]);
            sb.Append(' ');
            for (int i = 1; i < parts.Length; i++)
            {
                sb.Append(parts[i][0]).Append('.');
            }
            return sb.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
