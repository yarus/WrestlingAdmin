using System;
using System.Globalization;
using System.Windows.Data;
using Wrestling.Entities;

namespace Wrestling.UI.Material.Utils.Converters
{
    [ValueConversion(typeof(MatchWinTypeEnum), typeof(string))]
    public class WinTypeToStringConverter : IValueConverter
    {
        private const string TUSHEWIN = "Туше (VFA 5:0)";
        private const string INJURYWIN = "Травма (VIN 5:0)";
        private const string WARNINGSLIMIT = "3 предупреждения (VCA 5:0)";
        private const string NOSHOW = "Неявка (VFO 5:0)";
        private const string DISQUALIFYWIN = "Дисквалификация (DSQ 5:0)";
        private const string DOMINATIONWIN = "Преимущество (VSU 4:0)";
        private const string DOMINATIONWIN_WITH_POINTS = "Преимущество (VSU1 4:1)";
        private const string POINTSWIN = "Победа по Баллам (VPO 3:0)";
        private const string POINTSWIN_WITH_POINTS = "Победа по Баллам (VPO1 3:1)";
        private const string ACTIONWIN = "Последнее Действие (VPO1 3:1)";
        private const string FREEWIN = "Автопобеда";
        private const string MUTUAL_DSQ = "Обоюдная дисквал. (2DSQ 0:0)";
        private const string MUTUAL_INJURY = "Обоюдная травма (2VIN 0:0)";
        private const string MUTUAL_NOSHOW = "Обоюдная неявка (2VFO 0:0)";

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return string.Empty;

            var valueEnum = (MatchWinTypeEnum)Enum.Parse(typeof(MatchWinTypeEnum), value.ToString());

            switch (valueEnum)
            {
                case MatchWinTypeEnum.Tushe: return TUSHEWIN;
                case MatchWinTypeEnum.Injury: return INJURYWIN;
                case MatchWinTypeEnum.WarningsLimit: return WARNINGSLIMIT;
                case MatchWinTypeEnum.NoShow: return NOSHOW;
                case MatchWinTypeEnum.DisqualifyWin: return DISQUALIFYWIN;
                case MatchWinTypeEnum.DominationWin: return DOMINATIONWIN;
                case MatchWinTypeEnum.DominationWinWithPoints: return DOMINATIONWIN_WITH_POINTS;
                case MatchWinTypeEnum.PointsWin: return POINTSWIN;
                case MatchWinTypeEnum.PointsWinWithPoints: return POINTSWIN_WITH_POINTS;
                case MatchWinTypeEnum.ActionWin: return ACTIONWIN;
                case MatchWinTypeEnum.FreeWin: return FREEWIN;
                case MatchWinTypeEnum.MutualDisqualify: return MUTUAL_DSQ;
                case MatchWinTypeEnum.MutualInjury: return MUTUAL_INJURY;
                case MatchWinTypeEnum.MutualNoShow: return MUTUAL_NOSHOW;
            }

            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return null;

            switch (value.ToString())
            {
                case TUSHEWIN: return MatchWinTypeEnum.Tushe;
                case INJURYWIN: return MatchWinTypeEnum.Injury;
                case WARNINGSLIMIT: return MatchWinTypeEnum.WarningsLimit;
                case NOSHOW: return MatchWinTypeEnum.NoShow;
                case DISQUALIFYWIN: return MatchWinTypeEnum.DisqualifyWin;
                case DOMINATIONWIN: return MatchWinTypeEnum.DominationWin;
                case DOMINATIONWIN_WITH_POINTS: return MatchWinTypeEnum.DominationWinWithPoints;
                case POINTSWIN: return MatchWinTypeEnum.PointsWin;
                case POINTSWIN_WITH_POINTS: return MatchWinTypeEnum.PointsWinWithPoints;
                case ACTIONWIN: return MatchWinTypeEnum.ActionWin;
                case FREEWIN: return MatchWinTypeEnum.FreeWin;
                case MUTUAL_DSQ: return MatchWinTypeEnum.MutualDisqualify;
                case MUTUAL_INJURY: return MatchWinTypeEnum.MutualInjury;
                case MUTUAL_NOSHOW: return MatchWinTypeEnum.MutualNoShow;
            }

            return null;
        }
    }
}
