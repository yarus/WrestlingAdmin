using System;
using System.Globalization;
using System.Windows.Data;

namespace Wrestling.UI.Utils.Localization
{
    // Use with MultiBinding to apply a localization string as the format and
    // bind args after it: values[0] is the format ("Selected: {0}"), the
    // remaining values are the placeholder arguments. Lets us keep StringFormat
    // patterns in the JSON instead of hard-coding them in XAML.
    public class LocFormatConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length == 0) return string.Empty;

            var format = values[0] as string ?? string.Empty;
            if (values.Length == 1) return format;

            var args = new object[values.Length - 1];
            Array.Copy(values, 1, args, 0, args.Length);

            try
            {
                return string.Format(culture ?? CultureInfo.CurrentCulture, format, args);
            }
            catch (FormatException)
            {
                // Bad placeholder count in the JSON shouldn't crash the UI —
                // showing the unformatted raw template is debuggable enough.
                return format;
            }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
