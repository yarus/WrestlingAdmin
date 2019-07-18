using System;
using System.Globalization;
using System.Windows.Data;
using Wrestling.Entities;

namespace Wrestling.UI.Material.Utils.Converters
{
    public class WinTypeToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return string.Empty;

            var valueEnum = (MatchWinTypeEnum)Enum.Parse(typeof(MatchWinTypeEnum), value.ToString());

            switch (valueEnum)
            {
                case MatchWinTypeEnum.ActionWin: return "Последнее Действие";
                case MatchWinTypeEnum.DominationWin: return "Техническое Преимущество";
                case MatchWinTypeEnum.FreeWin: return "Автопобеда";
                case MatchWinTypeEnum.PointsWin: return "Победа по Очкам";
                case MatchWinTypeEnum.DisqualifyWin: return "Дисквалификация";
                case MatchWinTypeEnum.Tushe: return "Туше";
            }

            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return parameter;
        }
    }
}
