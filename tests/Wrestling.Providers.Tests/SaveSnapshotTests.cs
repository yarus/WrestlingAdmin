using System;
using System.Linq;
using FluentAssertions;
using Wrestling.Entities;
using Wrestling.Entities.Bracket;
using Xunit;

namespace Wrestling.Providers.Tests;

public class SaveSnapshotTests
{
    [Fact]
    public void Save_snapshot_keeps_match_versions_results_actions_and_membership_together()
    {
        var tournament = new Tournament(new GlobalSettings());
        var group = new AgeWeightGroup { ID = Guid.NewGuid() };
        tournament.Groups.Add(group);
        for (var i = 0; i < 2; i++)
        {
            var wrestler = new Wrestler
            {
                ID = Guid.NewGuid(), GroupID = group.ID, LastName = "Before",
                IsEntryFeePaid = true, IsWeightApproved = true, SeedNumber = i + 1
            };
            group.Wrestlers.Add(wrestler);
            tournament.Wrestlers.Add(wrestler);
        }
        var processor = new RoundRobinGroupBracketProcessor();
        processor.Generate(tournament, group);
        var match = group.Bracket.Rounds.SelectMany(r => r.RoundMatches).Single();
        match.MatchActions.Add(new MatchAction { Points = 2 });
        var mat = new Mat { ID = Guid.NewGuid() };
        mat.Groups.Add(group);
        tournament.Mats.Add(mat);
        var adapter = new EntityToInfoAdapter();

        var snapshot = adapter.GetInfoFromEntity(tournament);
        var savedGroup = snapshot.Groups.Single();
        var savedMat = snapshot.Mats.Single();
        processor.CompleteMatch(match, true, MatchWinTypeEnum.PointsWin);
        match.Version++;
        match.MatchActions[0].Points = 4;
        match.MatchActions.Add(new MatchAction { Points = 1 });
        tournament.Wrestlers[0].LastName = "After";
        mat.Groups.Clear();
        tournament.Groups.Clear();

        snapshot.Groups.Should().ContainSingle();
        var savedMatch = savedGroup.Bracket.Rounds.SelectMany(r => r.RoundMatches).Single();
        savedMatch.Status.Should().Be(MatchStatusEnum.Pending.ToString());
        savedMatch.Version.Should().Be(0);
        savedMatch.MatchActions.Select(a => a.Points).Should().Equal(2);
        snapshot.Wrestlers.Should().HaveCount(2).And.OnlyContain(w => w.LastName == "Before");
        savedMat.Groups.Should().Equal(group.ID);
    }
}
