using System;
using System.Globalization;
using System.Windows.Data;
using Wrestling.Entities;

namespace Wrestling.UI.Material.Tournament.Standing.Mats
{
    // Boolean form for use in DataTrigger — true when the group has any
    // "real" completed match (moving to another part would rewrite the
    // source part's standings). Auto-completed FreeWin byes don't count —
    // they carry no wrestled result. Mirrors MatRedistributionService's
    // CountCompleted so the disabled state matches the actual block.
    public sealed class GroupBlockedByCompletedBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var group = value as AgeWeightGroup;
            var rounds = group?.Bracket?.Rounds;
            if (rounds == null) return false;
            foreach (var round in rounds)
            {
                foreach (var match in round.RoundMatches)
                {
                    if (match.Status == MatchStatusEnum.Completed
                        && match.WinType != MatchWinTypeEnum.FreeWin)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
