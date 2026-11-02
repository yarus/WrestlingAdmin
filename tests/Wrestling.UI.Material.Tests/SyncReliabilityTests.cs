using System;
using System.Linq;
using FluentAssertions;
using Wrestling.Entities;
using Wrestling.Entities.Bracket;
using WTournament = Wrestling.Entities.Tournament;
using Wrestling.Providers;
using Wrestling.UI.Material.Model;
using Xunit;

namespace Wrestling.UI.Material.Tests;

public class SyncReliabilityTests
{
    private static WTournament Clone(WTournament t) => new EntityToInfoAdapter().GetEntityFromInfo(new EntityToInfoAdapter().GetInfoFromEntity(t));
    private static TournamentImporter Importer() => new(null, new GroupBracketProcessorFactory(), null);
    private static WrestlingMatch Match(AgeWeightGroup g, string key) => g.Bracket.Rounds.SelectMany(r => r.RoundMatches).Single(m => m.BracketFullNumber == key);

    private static WTournament CompletedTournament(int groups = 1)
    {
        var tournament = new WTournament(new GlobalSettings()) { ID = Guid.NewGuid(), Name = "Sync test" };
        for (int i = 0; i < groups; i++)
        {
            var group = new AgeWeightGroup { ID = Guid.NewGuid(), WeightMax = 60 };
            group.Wrestlers = Enumerable.Range(0, 8).Select(n => new Wrestler { ID = Guid.NewGuid(), GroupID = group.ID, FirstName = "W" + n, SeedNumber = n + 1 }).ToList();
            tournament.Groups.Add(group);
            foreach (var w in group.Wrestlers) tournament.Wrestlers.Add(w);
        }
        foreach (var g in tournament.Groups)
        {
            var processor = new OlympicGroupBracketProcessor();
            processor.Generate(tournament, g);
            while (g.Bracket.Rounds.SelectMany(r => r.RoundMatches).FirstOrDefault(m => m.IsMatchCanStart) is { } m)
            {
                processor.CompleteMatch(m, true, MatchWinTypeEnum.Tushe);
                m.Version++;
            }
        }
        return tournament;
    }

    [Fact]
    public void Newer_same_winner_score_edit_preserves_completed_descendants_and_match_instances()
    {
        var local = CompletedTournament();
        var remote = Clone(local);
        var match = Match(local.Groups[0], "1.1");
        var final = local.Groups[0].Bracket.Rounds.Last().RoundMatches[0];
        var edited = Match(remote.Groups[0], "1.1");
        edited.PointsRed = 4;
        edited.Version++;

        var result = Importer().Apply(local, ImportPlan.Proceed(remote));

        result.Outcome.Should().Be(ImportOutcome.Imported);
        match.PointsRed.Should().Be(4);
        Match(local.Groups[0], "1.1").Should().BeSameAs(match);
        final.Status.Should().Be(MatchStatusEnum.Completed);
        Importer().Apply(local, ImportPlan.Proceed(remote)).Outcome.Should().Be(ImportOutcome.NoNewData);
    }

    [Fact]
    public void Conflict_rolls_back_whole_category_but_other_category_is_imported()
    {
        var local = CompletedTournament(2);
        var remote = Clone(local);
        var first = remote.Groups[0];
        Match(first, "1.1").IsRedWon = false;
        Match(first, "1.1").Version++;
        first.FieldsVersion++;
        first.WeightMax = 99;
        Match(remote.Groups[1], "1.1").PointsRed = 2;
        Match(remote.Groups[1], "1.1").Version++;

        var result = Importer().Apply(local, ImportPlan.Proceed(remote));

        result.Outcome.ToString().Should().Be("Conflict");
        result.ImportedCount.Should().BeGreaterThan(0);
        local.Groups[0].WeightMax.Should().NotBe(99);
        Match(local.Groups[0], "1.1").IsRedWon.Should().BeTrue();
        Match(local.Groups[1], "1.1").PointsRed.Should().Be(2);
    }

    [Theory]
    [InlineData(MatchWinTypeEnum.NoShow)]
    [InlineData(MatchWinTypeEnum.DisqualifyWin)]
    public void Bracket_replacement_rebuilds_exclusion_flags_and_places(MatchWinTypeEnum type)
    {
        var local = CompletedTournament();
        var remote = Clone(local);
        var changed = Match(remote.Groups[0], "1.1");
        changed.WinType = type;
        changed.Version++;
        remote.Groups[0].BracketVersion++;
        var absentId = changed.WrestlerInBlue.ID;

        Importer().Apply(local, ImportPlan.Proceed(remote));

        var absent = local.Wrestlers.Single(w => w.ID == absentId);
        absent.IsNoShow.Should().Be(type == MatchWinTypeEnum.NoShow);
        absent.IsDisqualified.Should().Be(type == MatchWinTypeEnum.DisqualifyWin);
        absent.FinalPlace.Should().BeNull();
    }

    [Fact]
    public void Equal_version_different_result_reports_conflict_without_overwriting()
    {
        var local = CompletedTournament();
        var remote = Clone(local);
        Match(remote.Groups[0], "1.1").IsRedWon = false;

        var result = Importer().Apply(local, ImportPlan.Proceed(remote));

        result.Outcome.ToString().Should().Be("Conflict");
        Match(local.Groups[0], "1.1").IsRedWon.Should().BeTrue();
    }

    [Fact]
    public void Equal_matches_repair_derived_flags_from_accepted_local_results()
    {
        var local = CompletedTournament();
        var m = Match(local.Groups[0], "1.1");
        m.WinType = MatchWinTypeEnum.NoShow;
        m.WrestlerInBlue.IsNoShow = false;
        var remote = Clone(local);

        var result = Importer().Apply(local, ImportPlan.Proceed(remote));

        result.Outcome.Should().Be(ImportOutcome.Imported);
        m.WrestlerInBlue.IsNoShow.Should().BeTrue();
        m.WrestlerInBlue.FinalPlace.Should().BeNull();
    }

    [Fact]
    public void Incomplete_remote_result_does_not_abort_import_of_other_categories()
    {
        var local = CompletedTournament(2);
        var remote = Clone(local);
        Match(remote.Groups[0], "1.1").WinType = null;
        Match(remote.Groups[0], "1.1").Version++;
        Match(remote.Groups[1], "1.1").PointsRed = 5;
        Match(remote.Groups[1], "1.1").Version++;

        var result = Importer().Apply(local, ImportPlan.Proceed(remote));

        result.Outcome.Should().Be(ImportOutcome.Conflict);
        result.Conflicts.Single().Reason.Should().Be(ImportConflictReason.InvalidResult);
        Match(local.Groups[0], "1.1").WinType.Should().Be(MatchWinTypeEnum.Tushe);
        Match(local.Groups[1], "1.1").PointsRed.Should().Be(5);
    }
}
