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

                // Reset every match number first so stale values from previous runs cannot leak through
                // if a Bind* method fails to cover a particular match (the catch-all at the end will then
                // pick it up).
                foreach (var match in groups.SelectMany(g => g.Bracket.Rounds.SelectMany(r => r.RoundMatches)))
                {
                    match.MatchNumber = 0;
                }

                // Each WrestlingMatch instance can only be numbered once per Generate pass — protects against
                // shared references between rounds (e.g., a match accidentally referenced from both semi-final
                // and final rounds), which would otherwise yield two carpet matches sharing a MatchNumber.
                var assigned = new HashSet<WrestlingMatch>();

                BindMainBracketQualification(groups, processors, ref currentMatchNumber, assigned);

                BindSemiFinals(groups, processors, ref currentMatchNumber, assigned);

                BindAdditionalQualificationMatches(groups, processors, ref currentMatchNumber, assigned);

                BindThirdPlaceMatches(groups, processors, ref currentMatchNumber, assigned);

                BindFinalMatches(groups, processors, ref currentMatchNumber, assigned);

                // Catch-all: number any matches that were missed by Bind* (kept defensively in case a bracket
                // type adds a new round shape that the existing partition methods don't cover).
                foreach (var group in groups)
                {
                    foreach (var round in group.Bracket.Rounds)
                    {
                        foreach (var match in round.RoundMatches)
                        {
                            if (!assigned.Add(match)) continue;
                            match.MatchNumber = currentMatchNumber;
                            currentMatchNumber++;
                        }
                    }
                }
            }
        }

        private void BindMainBracketQualification(List<AgeWeightGroup> groups, List<IGroupBracketProcessor> processors, ref int currentMatchNumber, HashSet<WrestlingMatch> assigned)
        {
            int maxRound = groups.SelectMany(g => g.Bracket.Rounds).Where(r => r.RoundType == GroupRoundTypeEnum.Main).Max(r => r.RoundNumber);

            for (int i = 1; i <= maxRound; i++)
            {
                foreach (var group in groups)
                {
                    var processor = processors.FirstOrDefault(d => d.Code == group.Bracket.BracketTypeCode);
                    if (processor == null)
                    {
                        throw new InvalidOperationException($"No processor registered for bracket type '{group.Bracket.BracketTypeCode}'. Check DI registration in App.xaml.cs.");
                    }

                    var rounds = processor.GetMainQualificationRounds(group);

                    var round = rounds.FirstOrDefault(r => r.RoundNumber == i);
                    if (round != null)
                    {
                        foreach (var match in round.RoundMatches)
                        {
                            if (!assigned.Add(match)) continue;
                            match.MatchNumber = currentMatchNumber;
                            currentMatchNumber++;
                        }
                    }
                }
            }
        }

        private void BindSemiFinals(List<AgeWeightGroup> groups, List<IGroupBracketProcessor> processors, ref int currentMatchNumber, HashSet<WrestlingMatch> assigned)
        {
            // Bind semi-finals
            foreach (var group in groups)
            {
                var processor = processors.FirstOrDefault(d => d.Code == group.Bracket.BracketTypeCode);
                if (processor == null)
                {
                    throw new InvalidOperationException($"No processor registered for bracket type '{group.Bracket.BracketTypeCode}'. Check DI registration in App.xaml.cs.");
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
                    if (!assigned.Add(match)) continue;
                    match.MatchNumber = currentMatchNumber;
                    currentMatchNumber++;
                }
            }
        }

        private void BindAdditionalQualificationMatches(List<AgeWeightGroup> groups, List<IGroupBracketProcessor> processors, ref int currentMatchNumber, HashSet<WrestlingMatch> assigned)
        {
            var maxRounds = 0;

            foreach(var group in groups)
            {
                var processor = processors.FirstOrDefault(d => d.Code == group.Bracket.BracketTypeCode);
                if (processor == null)
                {
                    throw new InvalidOperationException($"No processor registered for bracket type '{group.Bracket.BracketTypeCode}'. Check DI registration in App.xaml.cs.");
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
                        throw new InvalidOperationException($"No processor registered for bracket type '{group.Bracket.BracketTypeCode}'. Check DI registration in App.xaml.cs.");
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
                            if (!assigned.Add(match)) continue;
                            match.MatchNumber = currentMatchNumber;
                            currentMatchNumber++;
                        }
                    }
                }
            }
        }

        private void BindThirdPlaceMatches(List<AgeWeightGroup> groups, List<IGroupBracketProcessor> processors, ref int currentMatchNumber, HashSet<WrestlingMatch> assigned)
        {
            foreach (var group in groups)
            {
                var processor = processors.FirstOrDefault(d => d.Code == group.Bracket.BracketTypeCode);
                if (processor == null)
                {
                    throw new InvalidOperationException($"No processor registered for bracket type '{group.Bracket.BracketTypeCode}'. Check DI registration in App.xaml.cs.");
                }

                var thirdPlaceRound = processor.Get3rdPlaceRound(group);
                if (thirdPlaceRound == null)
                {
                    continue;
                }

                foreach (var match in thirdPlaceRound.RoundMatches)
                {
                    if (!assigned.Add(match)) continue;
                    match.MatchNumber = currentMatchNumber;
                    currentMatchNumber++;
                }
            }
        }

        private void BindFinalMatches(List<AgeWeightGroup> groups, List<IGroupBracketProcessor> processors, ref int currentMatchNumber, HashSet<WrestlingMatch> assigned)
        {
            foreach (var group in groups)
            {
                var processor = processors.FirstOrDefault(d => d.Code == group.Bracket.BracketTypeCode);
                if (processor == null)
                {
                    throw new InvalidOperationException($"No processor registered for bracket type '{group.Bracket.BracketTypeCode}'. Check DI registration in App.xaml.cs.");
                }

                var finalRound = processor.GetFinalRound(group);
                if (finalRound == null)
                {
                    continue;
                }

                foreach (var match in finalRound.RoundMatches)
                {
                    if (!assigned.Add(match)) continue;
                    match.MatchNumber = currentMatchNumber;
                    currentMatchNumber++;
                }
            }
        }
    }
}
