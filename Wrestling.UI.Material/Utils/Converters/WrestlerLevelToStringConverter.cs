using System;
using System.Globalization;
using System.Windows.Data;
using Wrestling.Entities;

namespace Wrestling.UI.Material.Utils.Converters
{
    // Renders a WrestlerLevelEnum as its localized display label. Used by:
    //   - AddWrestlerDialog ComboBox ItemTemplate (to show "МСМК" / "MSMK"
    //     in Russian / English).
    //   - List/grid cells that bind to Wrestler.Level directly. Bindings
    //     that already target Wrestler.LevelDisplay don't need this converter.
    [ValueConversion(typeof(WrestlerLevelEnum), typeof(string))]
    public class WrestlerLevelToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is WrestlerLevelEnum lvl)
            {
                return WrestlerLevelLabels.GetDisplay(lvl);
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // ComboBox SelectedItem flows back as the enum value directly
            // (ItemsSource holds enum instances) — ConvertBack only fires when
            // a binding writes the displayed string back, which we don't do.
            throw new NotSupportedException();
        }
    }
}
