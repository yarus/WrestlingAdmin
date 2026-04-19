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
    public class BracketColumnTopMarginConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length != 2) return new Thickness(10, 0, 10, 0);

            var round = values[0] as GroupRound;
            var group = values[1] as AgeWeightGroup;

            if (round == null || group == null || group.Bracket == null) return new Thickness(10,0,10,0);

            var supportedBrackets = new List<IGroupBracketProcessor>
            {
                new OlympicGroupBracketProcessor(),
                new OlympicWithConsolationFromFinalistsGroupBracketProcessor(),
                new SubGroupsToOlympicBracketProcessor()
            };

            if (supportedBrackets.FirstOrDefault(g => g.Code == group.Bracket.BracketTypeCode) == null) return new Thickness(10, 0, 10, 0);

            int roundNumber = round.RoundNumber;

            if (round.RoundType == GroupRoundTypeEnum.Additional)
            {
                var mainCount = group.Bracket.Rounds.Count(r => r.RoundType == GroupRoundTypeEnum.Main);

                if (mainCount > 1 && round.RoundMatches.Count > 1)
                {
                    roundNumber = mainCount - 1;
                }
                else
                {
                    roundNumber = mainCount;
                }
            }

            int margin = ((int)Math.Pow(2, roundNumber-1) - 1) * 20;

            var isSub = group.Bracket.BracketTypeCode.ToLower() == BracketTypeEnum.SubGroupsIntoOlympic.ToString().ToLower();

            if (isSub && round.RoundType == GroupRoundTypeEnum.Main)
            {
                margin = 20;
            }

            return new Thickness(10, 0, 10, margin);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
