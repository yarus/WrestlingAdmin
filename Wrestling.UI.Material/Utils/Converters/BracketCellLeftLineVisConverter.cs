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
    public class BracketCellLeftLineVisConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length != 2) return Visibility.Hidden;

            var match = values[0] as WrestlingMatch;
            var group = values[1] as AgeWeightGroup;

            if (match == null || group == null || group.Bracket == null) return Visibility.Hidden;

            int mainRoundCount = group.Bracket.Rounds.Count(p => p.RoundType == GroupRoundTypeEnum.Main);
            int addRoundCound = group.Bracket.Rounds.Count(p => p.RoundType == GroupRoundTypeEnum.Additional);

            var supportedBrackets = new List<IGroupBracketProcessor>
            {
                new OlympicGroupBracketProcessor(),
                new OlympicWithConsilationFromFinalistsGroupBracketProcessor(),
                new SubGroupsToOlympicBracketPorcessor()
            };

            if (supportedBrackets.FirstOrDefault(g => g.Code == group.Bracket.BracketTypeCode) == null) return Visibility.Hidden;

            Visibility result;

            if (match.RoundNumber > mainRoundCount)
            {
                result = match.RoundNumber > mainRoundCount + 1 ? Visibility.Visible : Visibility.Hidden;

                if (group.Bracket.BracketTypeCode.ToLower() ==
                    BracketTypeEnum.SubGroupsIntoOlympic.ToString().ToLower() &&
                    match.RoundNumber == mainRoundCount + addRoundCound)
                {
                    result = Visibility.Hidden;
                }
            }
            else
            {
                result = (match.RoundNumber > 1 && group.Bracket.BracketTypeCode.ToLower() != BracketTypeEnum.SubGroupsIntoOlympic.ToString().ToLower()) ? Visibility.Visible : Visibility.Hidden;
            }

            return result;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}