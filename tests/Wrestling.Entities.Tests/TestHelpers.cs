using System;
using System.Collections.Generic;
using Wrestling.Entities;

namespace Wrestling.Entities.Tests;

internal static class TestHelpers
{
    public static Wrestler MakeWrestler(string last = "Иванов", string first = "Иван", int birthYear = 2005, double weight = 60, Guid? groupId = null, Guid? teamId = null)
    {
        return new Wrestler
        {
            ID = Guid.NewGuid(),
            LastName = last,
            FirstName = first,
            BirthDate = new DateTime(birthYear, 1, 1),
            Weight = weight,
            IsEntryFeePaid = true,
            IsWeightApproved = true,
            GroupID = groupId,
            TeamID = teamId
        };
    }

    public static AgeWeightGroup MakeGroup(int wrestlerCount, int maxRoundSecond = 180, int maxTimeoutSecond = 30, int maxActionSecond = 30)
    {
        var group = new AgeWeightGroup
        {
            ID = Guid.NewGuid(),
            BirthYearMin = 2005,
            BirthYearMax = 2006,
            WeightMax = 60,
            MaxRoundSecond = maxRoundSecond,
            MaxTimeoutSecond = maxTimeoutSecond,
            MaxActionSecond = maxActionSecond
        };

        var wrestlers = new List<Wrestler>();
        for (int i = 0; i < wrestlerCount; i++)
        {
            var w = MakeWrestler(last: $"W{i + 1:D2}", groupId: group.ID);
            w.SeedNumber = i + 1;
            wrestlers.Add(w);
        }

        group.Wrestlers = wrestlers;
        return group;
    }

    public static Tournament MakeTournament(params AgeWeightGroup[] groups)
    {
        var t = new Tournament(new GlobalSettings())
        {
            ID = Guid.NewGuid(),
            Name = "Test Cup",
            Status = TournamentStatus.InProgress
        };

        foreach (var g in groups)
        {
            t.Groups.Add(g);
            foreach (var w in g.Wrestlers)
            {
                t.Wrestlers.Add(w);
            }
        }

        return t;
    }
}
