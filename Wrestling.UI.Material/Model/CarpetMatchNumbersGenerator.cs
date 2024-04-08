using System;
using System.Collections.Generic;
using System.Linq;
using Wrestling.Entities;
using Wrestling.Entities.Bracket;

namespace Wrestling.UI.Material.Model
{
    // The Idea of this carpet generator is to have match numbers started from #1 for each Carpet.
    // So match numbers won't be unique during thru tournament but they will be unique for the Carpet.
    // This allows to easily predict match schedule for participants.
    public class CarpetMatchNumbersGenerator : IMatchNumbersGenerator
    {
        public void Generate(Entities.Tournament tournament, List<IGroupBracketProcessor> processors)
        {
            foreach (var carpet in tournament.Carpets)
            {
                int currentMatchNumber = 1;

                var groups = carpet.Groups.Where(g => g.Bracket != null).ToList();
                if (groups.Count == 0) continue;

                int maxRound = groups.SelectMany(g => g.Bracket.Rounds).Where(r => r.RoundType == GroupRoundTypeEnum.Main).Max(r => r.RoundNumber);

                BindMainBracketQualification(groups, processors, ref currentMatchNumber);

                BindSemiFinals(groups, processors, ref currentMatchNumber);

                BindAdditionalQualificationMatches(groups, processors, ref currentMatchNumber);

                BindThirdPlaceMatches(groups, processors, ref currentMatchNumber);

                BindFinalMatches(groups, processors, ref currentMatchNumber);                
            }
        }

        private void BindMainBracketQualification(List<AgeWeightGroup> groups, List<IGroupBracketProcessor> processors, ref int currentMatchNumber)
        {
            int maxRound = groups.SelectMany(g => g.Bracket.Rounds).Where(r => r.RoundType == GroupRoundTypeEnum.Main).Max(r => r.RoundNumber);

            for (int i = 1; i <= maxRound; i++)
            {
                foreach (var group in groups)
                {
                    var processor = processors.FirstOrDefault(d => d.Code == group.Bracket.BracketTypeCode);
                    if (processor == null)
                    {
                        throw new ApplicationException("No Processor registered for bracket type " + group.Bracket.BracketTypeCode);
                    }

                    var rounds = processor.GetMainQualificationRounds(group);

                    var round = rounds.FirstOrDefault(r => r.RoundNumber == i);
                    if (round != null)
                    {
                        foreach (var match in round.RoundMatches)
                        {
                            match.MatchNumber = currentMatchNumber;
                            currentMatchNumber++;
                        }
                    }
                }
            }
        }

        private void BindSemiFinals(List<AgeWeightGroup> groups, List<IGroupBracketProcessor> processors, ref int currentMatchNumber)
        {
            // Bind semi-finals
            foreach (var group in groups)
            {
                var processor = processors.FirstOrDefault(d => d.Code == group.Bracket.BracketTypeCode);
                if (processor == null)
                {
                    throw new ApplicationException("No Processor registered for bracket type " + group.Bracket.BracketTypeCode);
                }

                var semiFinalRound = processor.GetSemiFinalRound(group);
                if (semiFinalRound == null)
                {
                    // Group don't have semi-finals, just finals
                    // Note: RoundRobbin treat Count-2 match as the semi-final
                    continue;
                }

                foreach (var match in semiFinalRound.RoundMatches)
                {
                    match.MatchNumber = currentMatchNumber;
                    currentMatchNumber++;
                }
            }
        }

        private void BindAdditionalQualificationMatches(List<AgeWeightGroup> groups, List<IGroupBracketProcessor> processors, ref int currentMatchNumber)
        {
            var maxRounds = 0;

            foreach(var group in groups)
            {
                var processor = processors.FirstOrDefault(d => d.Code == group.Bracket.BracketTypeCode);
                if (processor == null)
                {
                    throw new ApplicationException("No Processor registered for bracket type " + group.Bracket.BracketTypeCode);
                }

                var addQualMatches = processor.GetAdditionalQualificationRounds(group);

                if (addQualMatches != null && addQualMatches.Count > 0 && addQualMatches.Count > maxRounds)
                {
                    maxRounds = addQualMatches.Count;
                }
            }

            for (int i = 0; i < maxRounds; i++)
            {
                foreach (var group in groups)
                {
                    var processor = processors.FirstOrDefault(d => d.Code == group.Bracket.BracketTypeCode);
                    if (processor == null)
                    {
                        throw new ApplicationException("No Processor registered for bracket type " + group.Bracket.BracketTypeCode);
                    }

                    var addQualRounds = processor.GetAdditionalQualificationRounds(group);

                    if (addQualRounds == null || addQualRounds.Count < (i+1))
                    {
                        continue;
                    }

                    var round = addQualRounds[i];
                    if (round != null)
                    {
                        foreach (var match in round.RoundMatches)
                        {
                            match.MatchNumber = currentMatchNumber;
                            currentMatchNumber++;
                        }
                    }
                }
            }
        }

        private void BindThirdPlaceMatches(List<AgeWeightGroup> groups, List<IGroupBracketProcessor> processors, ref int currentMatchNumber)
        {            
            foreach (var group in groups)
            {
                var processor = processors.FirstOrDefault(d => d.Code == group.Bracket.BracketTypeCode);
                if (processor == null)
                {
                    throw new ApplicationException("No Processor registered for bracket type " + group.Bracket.BracketTypeCode);
                }

                var thirdPlaceRound = processor.Get3rdPlaceRound(group);
                if (thirdPlaceRound == null)
                {
                    continue;
                }

                foreach (var match in thirdPlaceRound.RoundMatches)
                {
                    match.MatchNumber = currentMatchNumber;
                    currentMatchNumber++;
                }
            }
        }

        private void BindFinalMatches(List<AgeWeightGroup> groups, List<IGroupBracketProcessor> processors, ref int currentMatchNumber)
        {
            foreach (var group in groups)
            {
                var processor = processors.FirstOrDefault(d => d.Code == group.Bracket.BracketTypeCode);
                if (processor == null)
                {
                    throw new ApplicationException("No Processor registered for bracket type " + group.Bracket.BracketTypeCode);
                }

                var finalRound = processor.GetFinalRound(group);
                if (finalRound == null)
                {
                    continue;
                }

                foreach (var match in finalRound.RoundMatches)
                {
                    match.MatchNumber = currentMatchNumber;
                    currentMatchNumber++;
                }
            }
        }
    }
}