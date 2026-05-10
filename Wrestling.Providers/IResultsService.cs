using System;
using System.Collections.Generic;
using Wrestling.Entities;
using Wrestling.Entities.Results;

namespace Wrestling.Providers
{
    public interface IResultsService
    {
        IReadOnlyList<TournamentResult> AllResults { get; }
        IReadOnlyList<TournamentTeamResult> TeamResults { get; }
        IReadOnlyList<WrestlerAchievement> Achievements { get; }

        event Action ResultsChanged;

        void Recalculate(Tournament tournament);

        // Returns the team standings reordered by the given orderer. Cached
        // per orderer instance and invalidated on Recalculate, so views that
        // bind to a specific ranking system (Olympic / Medals / Points) don't
        // re-sort on every refresh. Passing a null orderer returns the
        // unordered base list.
        IReadOnlyList<TournamentTeamResult> GetOrderedTeamResults(ITeamResultsOrderer orderer);
    }
}
