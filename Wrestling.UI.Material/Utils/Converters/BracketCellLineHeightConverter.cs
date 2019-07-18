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
    public class BracketCellLineHeightConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length != 2) return new GridLength(0);

            var match = values[0] as WrestlingMatch;
            var group = values[1] as AgeWeightGroup;

            if (match == null || group == null || group.Bracket == null || match.GroupID != group.ID) return new GridLength(0);

            int lastMainRound = group.Bracket.Rounds.Count(p => p.RoundType == GroupRoundTypeEnum.Main);

            bool isLastMatchInRound = match.BracketNumber == group.Bracket.Rounds[match.RoundNumber - 1].RoundMatches.Count;

            // Need only 40 for Additional Rounds
            //if (match.RoundNumber > lastMainRound)
            //{
                //return isLastMatchInRound ? new GridLength(0) : new GridLength(40);
            //}

            var supportedBrackets = new List<IGroupBracketProcessor>
            {
                new OlympicGroupBracketProcessor(),
                new OlympicWithConsilationFromFinalistsGroupBracketProcessor(),
                new SubGroupsToOlympicBracketPorcessor()
            };

            if (supportedBrackets.FirstOrDefault(g => g.Code == group.Bracket.BracketTypeCode) == null) return new GridLength(0);

            int roundNumber = match.RoundNumber;

            if (roundNumber > lastMainRound)
            {
                if (lastMainRound > 1)
                {
                    roundNumber = lastMainRound - 1;
                }
                else
                {
                    roundNumber = lastMainRound;
                }
            }

            int height = ((int)Math.Pow(2, roundNumber-1) - 1) * 40;

            if (group.Bracket.BracketTypeCode.ToLower() == BracketTypeEnum.SubGroupsIntoOlympic.ToString().ToLower())
            {
                var round = group.Bracket.Rounds.FirstOrDefault(r => r.RoundNumber == match.RoundNumber);

                if (round != null && round.RoundType == GroupRoundTypeEnum.Main)
                {
                    height = 0;
                }
            }

            return isLastMatchInRound ? new GridLength(0) : new GridLength(height, GridUnitType.Pixel);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
