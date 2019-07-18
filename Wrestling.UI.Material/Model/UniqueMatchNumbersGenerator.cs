using System.Linq;
using Wrestling.Entities;

namespace Wrestling.UI.Material.Model
{
    public class UniqueMatchNumbersGenerator : IMatchNumbersGenerator
    {
        public void Generate(Entities.Tournament tournament)
        {
            var groupList = tournament.Carpets.SelectMany(c => c.Groups).ToList();

            int currentMatchNumber = 1;

            var groups = groupList.Where(g => g.Bracket != null).ToList();
            if (groups.Count == 0) return;

            int maxRound = groups.SelectMany(g => g.Bracket.Rounds).Where(r => r.RoundType == GroupRoundTypeEnum.Main).Max(r => r.RoundNumber);

            // Bind main matches except finals
            for (int i = 1; i <= maxRound; i++)
            {
                foreach (var group in groups)
                {
                    int maxMainRound = group.Bracket.Rounds.Count(r => r.RoundType == GroupRoundTypeEnum.Main);

                    if (i >= maxMainRound) continue;

                    var round = group.Bracket.Rounds.FirstOrDefault(r => r.RoundNumber == i);
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
            // Bind additional matches
            var additionalRounds = groups.SelectMany(g => g.Bracket.Rounds).Where(r => r.RoundType == GroupRoundTypeEnum.Additional).ToList();
            if (additionalRounds.Count > 0)
            {
                var maxAddRound = additionalRounds.Max(r => r.RoundNumber);
                var minAddRound = additionalRounds.Min(r => r.RoundNumber);

                for (int i = minAddRound; i <= maxAddRound; i++)
                {
                    foreach (var group in groups)
                    {
                        var round = group.Bracket.Rounds.FirstOrDefault(r => r.RoundType == GroupRoundTypeEnum.Additional && r.RoundNumber == i);
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

            // Bind finals
            foreach (var group in groups)
            {
                var finalRound = group.Bracket.Rounds.FirstOrDefault(r => r.RoundType == GroupRoundTypeEnum.Main && r.RoundNumber == group.Bracket.Rounds.Count(x => x.RoundType == GroupRoundTypeEnum.Main));
                if (finalRound != null)
                {
                    foreach (var match in finalRound.RoundMatches)
                    {
                        match.MatchNumber = currentMatchNumber;
                        currentMatchNumber++;
                    }
                }
            }
        }
    }
}
