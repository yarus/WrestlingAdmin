using System;
using System.Globalization;
using System.Windows.Data;
using Wrestling.Entities;

namespace Wrestling.UI.Material.Utils.Converters
{
    [ValueConversion(typeof(MatchWinTypeEnum), typeof(string))]
    public class WinTypeToStringConverter : IValueConverter
    {
        private const string ACTIONWIN = "Последнее Действие";
        private const string DOMINATIONWIN = "Техническое Преимущество";
        private const string FREEWIN = "Автопобеда";
        private const string POINTSWIN = "Победа по Баллам";
        private const string DISQUALIFYWIN = "Дисквалификация";
        private const string TUSHEWIN = "Победа по Туше";

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return string.Empty;

            var valueEnum = (MatchWinTypeEnum)Enum.Parse(typeof(MatchWinTypeEnum), value.ToString());

            switch (valueEnum)
            {
                case MatchWinTypeEnum.ActionWin: return ACTIONWIN;
                case MatchWinTypeEnum.DominationWin: return DOMINATIONWIN;
                case MatchWinTypeEnum.FreeWin: return FREEWIN;
                case MatchWinTypeEnum.PointsWin: return POINTSWIN;
                case MatchWinTypeEnum.DisqualifyWin: return DISQUALIFYWIN;
                case MatchWinTypeEnum.Tushe: return TUSHEWIN;
            }

            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return null;

            switch (value.ToString())
            {
                case ACTIONWIN: return MatchWinTypeEnum.ActionWin;
                case DOMINATIONWIN: return MatchWinTypeEnum.DominationWin;
                case FREEWIN: return MatchWinTypeEnum.FreeWin;
                case POINTSWIN: return MatchWinTypeEnum.PointsWin;
                case DISQUALIFYWIN: return MatchWinTypeEnum.DisqualifyWin;
                case TUSHEWIN: return MatchWinTypeEnum.Tushe;
            }

            return null;
        }
    }
}
