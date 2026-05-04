using System;
using System.Collections.Generic;
using Wrestling.Entities;
using Wrestling.Entities.Results;
using Wrestling.Entities.Results.Achievements;

namespace Wrestling.Providers
{
    public interface IResultsService
    {
        IReadOnlyList<TournamentResult> AllResults { get; }
        IReadOnlyList<TournamentTeamResult> TeamResults { get; }
        IReadOnlyList<WrestlerAchievement> Achievements { get; }

        event Action ResultsChanged;

        void Recalculate(Tournament tournament);
    }
}
