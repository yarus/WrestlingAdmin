using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Data;
using Wrestling.Entities;
using Wrestling.UI.Utils.Localization;

namespace Wrestling.UI.Material.Utils.Converters
{
    [ValueConversion(typeof(MatchWinTypeEnum), typeof(string))]
    public class WinTypeToStringConverter : IValueConverter
    {
        // Stable mapping enum→localization key. The displayed string comes
        // from LocalizationService at Convert time, so a language switch flips
        // every "win type" label without any per-binding plumbing.
        private static readonly Dictionary<MatchWinTypeEnum, string> KeyByEnum = new Dictionary<MatchWinTypeEnum, string>
        {
            { MatchWinTypeEnum.Tushe,                    "WinType_Tushe" },
            { MatchWinTypeEnum.Injury,                   "WinType_Injury" },
            { MatchWinTypeEnum.WarningsLimit,            "WinType_WarningsLimit" },
            { MatchWinTypeEnum.NoShow,                   "WinType_NoShow" },
            { MatchWinTypeEnum.DisqualifyWin,            "WinType_DisqualifyWin" },
            { MatchWinTypeEnum.DominationWin,            "WinType_DominationWin" },
            { MatchWinTypeEnum.DominationWinWithPoints,  "WinType_DominationWinWithPoints" },
            { MatchWinTypeEnum.PointsWin,                "WinType_PointsWin" },
            { MatchWinTypeEnum.PointsWinWithPoints,      "WinType_PointsWinWithPoints" },
            { MatchWinTypeEnum.ActionWin,                "WinType_ActionWin" },
            { MatchWinTypeEnum.FreeWin,                  "WinType_FreeWin" },
            { MatchWinTypeEnum.MutualDisqualify,         "WinType_MutualDisqualify" },
            { MatchWinTypeEnum.MutualInjury,             "WinType_MutualInjury" },
            { MatchWinTypeEnum.MutualNoShow,             "WinType_MutualNoShow" },
        };

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return string.Empty;

            var valueEnum = (MatchWinTypeEnum)Enum.Parse(typeof(MatchWinTypeEnum), value.ToString());
            return KeyByEnum.TryGetValue(valueEnum, out var key)
                ? LocalizationService.Instance.T(key)
                : string.Empty;
        }

        // Round-trip: map the displayed string back through the current
        // language's lookup table. If two enums ever produced the same
        // localized text the first match wins — the canonical strings here
        // are unique by design (each carries its UWW code in parentheses).
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return null;

            var text = value.ToString();
            foreach (var pair in KeyByEnum)
            {
                if (string.Equals(LocalizationService.Instance.T(pair.Value), text, StringComparison.Ordinal))
                {
                    return pair.Key;
                }
            }
            return null;
        }
    }
}
