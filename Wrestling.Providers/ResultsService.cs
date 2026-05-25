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

        // Per-(orderer, part) cache. Orderers are DI singletons so reference
        // equality on the orderer component is sufficient; partId is value-
        // compared. Cleared on every Recalculate so stale orderings never
        // escape past a match approve.
        private readonly Dictionary<(ITeamResultsOrderer, Guid?), IReadOnlyList<TournamentTeamResult>> _orderedTeamCache
            = new Dictionary<(ITeamResultsOrderer, Guid?), IReadOnlyList<TournamentTeamResult>>();

        // Unordered per-part team standings, built lazily from AllResults on
        // first request for a part and cleared alongside the ordered cache.
        private readonly Dictionary<Guid, IReadOnlyList<TournamentTeamResult>> _partTeamCache
            = new Dictionary<Guid, IReadOnlyList<TournamentTeamResult>>();

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
            _orderedTeamCache.Clear();
            _partTeamCache.Clear();

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

        public IReadOnlyList<TournamentTeamResult> GetOrderedTeamResults(ITeamResultsOrderer orderer)
            => GetOrderedTeamResults(orderer, null);

        public IReadOnlyList<TournamentTeamResult> GetOrderedTeamResults(ITeamResultsOrderer orderer, Guid? partId)
        {
            var baseResults = partId.HasValue ? GetTeamResultsForPart(partId.Value) : TeamResults;
            if (baseResults.Count == 0) return baseResults;
            if (orderer == null) return baseResults;

            var key = (orderer, partId);
            if (_orderedTeamCache.TryGetValue(key, out var cached)) return cached;

            var ordered = orderer.GetOrderedResults(baseResults.ToList()) ?? new List<TournamentTeamResult>();
            _orderedTeamCache[key] = ordered;
            return ordered;
        }

        // Team standings scoped to one part: only personal results whose group
        // belongs to that part feed the aggregation, so a team with no wrestler
        // in the part simply doesn't appear (the calculator builds rows from
        // the supplied inputs).
        private IReadOnlyList<TournamentTeamResult> GetTeamResultsForPart(Guid partId)
        {
            if (_teamCalculator == null) return EmptyTeamResults;
            if (_partTeamCache.TryGetValue(partId, out var cached)) return cached;

            var partResults = AllResults
                .Where(r => r.Group != null && r.Group.PartID == partId)
                .ToList();
            var teams = _teamCalculator.GetTeamResults(partResults, null) ?? new List<TournamentTeamResult>();
            _partTeamCache[partId] = teams;
            return teams;
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

            // DSQ'd / no-show wrestlers (FinalPlace == null) go to the bottom
            // of each weight category. Without IsPlaceless / null-coalesce
            // tiebreakers, default null-first ordering puts them above the
            // gold medalist. LastName is the final stable tiebreaker so
            // re-runs produce a deterministic list.
            return tmpResults
                .OrderBy(x => x.Group.Name)
                .ThenBy(p => p.Wrestler.IsPlaceless)
                .ThenBy(p => p.Wrestler.FinalPlace ?? int.MaxValue)
                .ThenBy(p => p.Wrestler.LastName)
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
