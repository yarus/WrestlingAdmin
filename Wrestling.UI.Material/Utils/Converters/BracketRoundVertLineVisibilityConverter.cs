using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using Wrestling.Entities;
using Wrestling.Entities.Bracket;

namespace Wrestling.UI.Material.Utils.Converters
{
    public class BracketRoundVertLineVisibilityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length != 3) return Visibility.Hidden;

            var supportedBrackets = new List<IGroupBracketProcessor>
            {
                new OlympicGroupBracketProcessor(),
                new OlympicWithConsolationFromFinalistsGroupBracketProcessor(),
                new SubGroupsToOlympicBracketProcessor()
            };

            var match = values[0] as WrestlingMatch;
            var round = values[1] as GroupRound;
            var group = values[2] as AgeWeightGroup;

            if (round == null || match == null || group == null || group.Bracket == null) return Visibility.Hidden;

            var isSub = group.Bracket.BracketTypeCode.ToLower() == BracketTypeEnum.SubGroupsIntoOlympic.ToString().ToLower();

            if (((!isSub && round.RoundType == GroupRoundTypeEnum.Additional) || (isSub && round.RoundType == GroupRoundTypeEnum.Main))) return Visibility.Hidden;

            var isLastMatchInRound = round.RoundMatches.IndexOf(match) == round.RoundMatches.Count - 1;
            var isApplicable = supportedBrackets.FirstOrDefault(b => b.Code == group.Bracket.BracketTypeCode) != null;

            return isApplicable && !isLastMatchInRound && match.BracketNumber % 2 != 0 ? Visibility.Visible : Visibility.Hidden;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
