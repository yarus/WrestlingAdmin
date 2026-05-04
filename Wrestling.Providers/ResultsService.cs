using System;
using System.Collections.Generic;
using System.Linq;
using Wrestling.Entities;
using Wrestling.Entities.Bracket;
using Wrestling.Entities.Results;
using Wrestling.Entities.Results.Achievements;

namespace Wrestling.Providers
{
    public class ResultsService : IResultsService
    {
        private static readonly IReadOnlyList<TournamentResult> EmptyResults = new List<TournamentResult>();
        private static readonly IReadOnlyList<TournamentTeamResult> EmptyTeamResults = new List<TournamentTeamResult>();
        private static readonly IReadOnlyList<WrestlerAchievement> EmptyAchievements = new List<WrestlerAchievement>();

        private readonly List<IGroupBracketProcessor> _bracketProcessors;
        private readonly ITeamResultsCalculator _teamCalculator;
        private readonly List<IAchievementCalculator> _achievementCalculators;

        public ResultsService(
            List<IGroupBracketProcessor> bracketProcessors,
            ITeamResultsCalculator teamCalculator,
            List<IAchievementCalculator> achievementCalculators)
        {
            _bracketProcessors = bracketProcessors ?? new List<IGroupBracketProcessor>();
            _teamCalculator = teamCalculator;
            _achievementCalculators = achievementCalculators ?? new List<IAchievementCalculator>();

            AllResults = EmptyResults;
            TeamResults = EmptyTeamResults;
            Achievements = EmptyAchievements;
        }

        public IReadOnlyList<TournamentResult> AllResults { get; private set; }
        public IReadOnlyList<TournamentTeamResult> TeamResults { get; private set; }
        public IReadOnlyList<WrestlerAchievement> Achievements { get; private set; }

        public event Action ResultsChanged;

        public void Recalculate(Tournament tournament)
        {
            if (tournament == null)
            {
                AllResults = EmptyResults;
                TeamResults = EmptyTeamResults;
                Achievements = EmptyAchievements;
                RaiseChanged();
                return;
            }

            AllResults = CalculateAllResults(tournament);
            TeamResults = _teamCalculator != null
                ? _teamCalculator.GetTeamResults(AllResults.ToList(), null) ?? new List<TournamentTeamResult>()
                : EmptyTeamResults;
            Achievements = CalculateAchievements(tournament, AllResults);

            RaiseChanged();
        }

        private List<TournamentResult> CalculateAllResults(Tournament tournament)
        {
            var tmpResults = new List<TournamentResult>();

            foreach (var group in tournament.Groups)
            {
                if (group.Bracket == null) continue;

                var processor = _bracketProcessors.FirstOrDefault(p => p.Code == group.Bracket.BracketTypeCode);
                if (processor == null) continue;

                processor.Load(tournament, group);
                var results = processor.GetResults();
                if (results != null)
                {
                    tmpResults.AddRange(results);
                }
            }

            return tmpResults
                .OrderBy(x => x.Group.Name)
                .ThenBy(p => p.Wrestler.FinalPlace)
                .ToList();
        }

        private List<WrestlerAchievement> CalculateAchievements(Tournament tournament, IReadOnlyList<TournamentResult> allResults)
        {
            var achievements = new List<WrestlerAchievement>();
            var resultsList = allResults.ToList();

            foreach (var calc in _achievementCalculators)
            {
                var results = calc.CalculateAchievement(tournament, resultsList);
                if (results != null && results.Count > 0)
                {
                    achievements.AddRange(results);
                }
            }

            return achievements;
        }

        private void RaiseChanged()
        {
            ResultsChanged?.Invoke();
        }
    }
}
