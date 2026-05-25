using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Wrestling.Entities;
using Wrestling.Entities.Bracket;
using Wrestling.Entities.Results;
using Wrestling.Entities.Results.Achievements;
using Xunit;

namespace Wrestling.Providers.Tests;

// Behavior contract for the event-driven results cache (replaces lazy
// per-VM recalculation in the old ResultsViewModel):
//   - Recalculate(null) clears all three collections and raises ResultsChanged.
//   - Recalculate(tournament) runs every group through its bracket processor,
//     aggregates team standings via ITeamResultsCalculator, and runs every
//     IAchievementCalculator.
//   - Groups without a Bracket or without a matching processor are skipped
//     (instead of throwing) so a partially-set-up tournament still produces
//     usable results for the configured groups.
public sealed class ResultsServiceTests
{
    [Fact]
    public void Recalculate_null_clears_caches_and_raises_event()
    {
        var svc = MakeService();
        var raised = 0;
        svc.ResultsChanged += () => raised++;

        svc.Recalculate(null);

        svc.AllResults.Should().BeEmpty();
        svc.TeamResults.Should().BeEmpty();
        svc.Achievements.Should().BeEmpty();
        raised.Should().Be(1);
    }

    [Fact]
    public void Recalculate_olympic4_populates_results_and_raises_event()
    {
        var (svc, tournament) = BuildOlympic4();
        var raised = 0;
        svc.ResultsChanged += () => raised++;

        svc.Recalculate(tournament);

        raised.Should().Be(1);
        svc.AllResults.Should().HaveCount(4);
        svc.AllResults.Select(r => r.Wrestler.FinalPlace)
            .Should().BeEquivalentTo(new int?[] { 1, 2, 3, 4 });
    }

    [Fact]
    public void Recalculate_orders_results_by_group_name_then_final_place()
    {
        var (svc, tournament) = BuildTwoGroupsOlympic4();

        svc.Recalculate(tournament);

        // Two groups, 4 wrestlers each = 8 results, ordered by group then place.
        svc.AllResults.Should().HaveCount(8);
        var groupNames = svc.AllResults.Select(r => r.Group.Name).Distinct().ToList();
        groupNames.Should().HaveCount(2);

        // Inside each group, FinalPlace must be 1,2,3,4 in order.
        foreach (var groupName in groupNames)
        {
            svc.AllResults
                .Where(r => r.Group.Name == groupName)
                .Select(r => r.Wrestler.FinalPlace)
                .Should().BeEquivalentTo(new int?[] { 1, 2, 3, 4 });
        }
    }

    [Fact]
    public void Recalculate_skips_groups_without_brackets()
    {
        var (svc, tournament) = BuildOlympic4();
        // Add a second group with no bracket — it must not throw.
        var bracketless = MakeGroup("без сетки", null, null, 100);
        bracketless.Wrestlers.Add(MakeWrestler("Без", "Сетки"));
        tournament.Groups.Add(bracketless);

        Action act = () => svc.Recalculate(tournament);

        act.Should().NotThrow();
        svc.AllResults.Should().HaveCount(4); // bracketless group contributes nothing
    }

    [Fact]
    public void Recalculate_skips_groups_when_no_matching_processor_registered()
    {
        // Build the service WITHOUT the OlympicGroupBracketProcessor registered.
        var calc = new TeamResultsCalculator();
        var svc = new ResultsService(
            new List<IGroupBracketProcessor>(),  // empty — nothing matches
            calc,
            new List<IAchievementCalculator>());

        var tournament = MakeTournamentOlympic4();

        Action act = () => svc.Recalculate(tournament);

        act.Should().NotThrow();
        svc.AllResults.Should().BeEmpty();
        svc.TeamResults.Should().BeEmpty();
    }

    [Fact]
    public void Recalculate_aggregates_team_results_per_team()
    {
        var (svc, tournament) = BuildOlympic4WithTeams(redTeam: "Red", blueTeam: "Blue");

        svc.Recalculate(tournament);

        // Two distinct teams from 4 wrestlers (alternating).
        svc.TeamResults.Should().HaveCount(2);
        svc.TeamResults.Sum(t => t.Wrestlers.Count).Should().Be(4);
        svc.TeamResults.Sum(t => t.GoldMedals).Should().Be(1);
        svc.TeamResults.Sum(t => t.SilverMedals).Should().Be(1);
        svc.TeamResults.Sum(t => t.BronzeMedals).Should().Be(1);
    }

    [Fact]
    public void Recalculate_runs_registered_achievement_calculators()
    {
        var stub = new StubAchievementCalculator();
        var calc = new TeamResultsCalculator();
        var svc = new ResultsService(
            new List<IGroupBracketProcessor> { new OlympicGroupBracketProcessor() },
            calc,
            new List<IAchievementCalculator> { stub });

        var tournament = MakeTournamentOlympic4();

        svc.Recalculate(tournament);

        stub.InvokedCount.Should().Be(1);
        svc.Achievements.Should().ContainSingle(a => a.Title == "stub");
    }

    [Fact]
    public void Recalculate_replaces_previous_cache_each_call()
    {
        var (svc, tournament) = BuildOlympic4();

        svc.Recalculate(tournament);
        var first = svc.AllResults;
        first.Should().NotBeEmpty();

        svc.Recalculate(null);
        svc.AllResults.Should().BeEmpty();

        svc.Recalculate(tournament);
        svc.AllResults.Should().NotBeEmpty();
        svc.AllResults.Should().NotBeSameAs(first);
    }

    [Fact]
    public void GetOrderedTeamResults_with_partId_scopes_team_results_to_that_part()
    {
        // Two groups in two different parts. Each group has 4 wrestlers split
        // across the same two teams (Red/Blue). Whole-tournament aggregation
        // sees all 8 wrestlers; a per-part view sees only that part's 4.
        var svc = MakeService();
        var tournament = new Tournament(new GlobalSettings()) { ID = Guid.NewGuid(), Name = "T" };

        var part1 = new TournamentPart { ID = Guid.NewGuid(), Name = "Часть 1" };
        var part2 = new TournamentPart { ID = Guid.NewGuid(), Name = "Часть 2" };
        tournament.Parts.Clear();
        tournament.Parts.Add(part1);
        tournament.Parts.Add(part2);

        AppendOlympicGroup(tournament, "до 50 кг", weightMax: 50, redTeam: "Red", blueTeam: "Blue");
        AppendOlympicGroup(tournament, "до 60 кг", weightMax: 60, redTeam: "Red", blueTeam: "Blue");
        tournament.Groups[0].PartID = part1.ID;
        tournament.Groups[1].PartID = part2.ID;

        svc.Recalculate(tournament);

        // Sum across all parts: both teams, 8 wrestlers, 2 golds total.
        var all = svc.GetOrderedTeamResults(null, null);
        all.Sum(t => t.Wrestlers.Count).Should().Be(8);
        all.Sum(t => t.GoldMedals).Should().Be(2);

        // Part 1 only: 4 wrestlers, 1 gold.
        var p1 = svc.GetOrderedTeamResults(null, part1.ID);
        p1.Sum(t => t.Wrestlers.Count).Should().Be(4);
        p1.Sum(t => t.GoldMedals).Should().Be(1);

        // Part 2 only: the other 4 wrestlers, 1 gold.
        var p2 = svc.GetOrderedTeamResults(null, part2.ID);
        p2.Sum(t => t.Wrestlers.Count).Should().Be(4);
        p2.Sum(t => t.GoldMedals).Should().Be(1);
    }

    [Fact]
    public void Achievements_are_empty_until_a_decisive_result_exists()
    {
        // Bracket drawn but no match decided — the same zero-metric state a
        // FreeWin bye produces (byes carry no win type / points / actions). No
        // laureate should be crowned; otherwise the whole field ties at 0 and
        // every wrestler shows up as a nominee (the reported bug).
        var svc = new ResultsService(
            new List<IGroupBracketProcessor> { new OlympicGroupBracketProcessor() },
            new TeamResultsCalculator(),
            RealAchievementCalculators());

        var tournament = new Tournament(new GlobalSettings()) { ID = Guid.NewGuid(), Name = "T" };
        var group = MakeGroup("до 60 кг", birthYearMin: 2010, birthYearMax: 2011, weightMax: 60);
        for (int i = 0; i < 4; i++)
        {
            var w = MakeWrestler($"W{i + 1:D2}", "Test");
            w.GroupID = group.ID;
            w.SeedNumber = i + 1;
            group.Wrestlers.Add(w);
            tournament.Wrestlers.Add(w);
        }
        tournament.Groups.Add(group);
        new OlympicGroupBracketProcessor().Generate(tournament, group);

        svc.Recalculate(tournament);

        svc.Achievements.Should().BeEmpty();
    }

    [Fact]
    public void Achievement_crowns_only_real_winners_not_the_whole_field()
    {
        // Four wrestlers, all matches won by tushe. Only the champion racks up
        // 2 tushe wins, so the «most tushe» nomination must list exactly one
        // wrestler — not everyone tied below them.
        var svc = new ResultsService(
            new List<IGroupBracketProcessor> { new OlympicGroupBracketProcessor() },
            new TeamResultsCalculator(),
            RealAchievementCalculators());

        var tournament = new Tournament(new GlobalSettings()) { ID = Guid.NewGuid(), Name = "T" };
        AppendOlympicGroup(tournament, "до 60 кг", weightMax: 60, winType: MatchWinTypeEnum.Tushe);

        svc.Recalculate(tournament);

        var tushe = svc.Achievements.Where(a => a.AchievementType == "MostTusheWinsCount").ToList();
        tushe.Should().ContainSingle();
        tushe[0].AchievementValue.Should().Be("2");
    }

    // --- Helpers -----------------------------------------------------------

    private static ResultsService MakeService()
    {
        return new ResultsService(
            new List<IGroupBracketProcessor> { new OlympicGroupBracketProcessor() },
            new TeamResultsCalculator(),
            new List<IAchievementCalculator>());
    }

    private static (ResultsService svc, Tournament tournament) BuildOlympic4()
    {
        var svc = MakeService();
        var tournament = MakeTournamentOlympic4();
        return (svc, tournament);
    }

    private static (ResultsService svc, Tournament tournament) BuildOlympic4WithTeams(string redTeam, string blueTeam)
    {
        var svc = MakeService();
        var tournament = MakeTournamentOlympic4(redTeam, blueTeam);
        return (svc, tournament);
    }

    private static (ResultsService svc, Tournament tournament) BuildTwoGroupsOlympic4()
    {
        var svc = MakeService();
        var tournament = new Tournament(new GlobalSettings()) { ID = Guid.NewGuid(), Name = "T" };

        AppendOlympicGroup(tournament, "до 50 кг", weightMax: 50);
        AppendOlympicGroup(tournament, "до 60 кг", weightMax: 60);

        return (svc, tournament);
    }

    private static Tournament MakeTournamentOlympic4(string redTeam = null, string blueTeam = null)
    {
        var tournament = new Tournament(new GlobalSettings()) { ID = Guid.NewGuid(), Name = "T" };
        AppendOlympicGroup(tournament, "до 60 кг", weightMax: 60, redTeam: redTeam, blueTeam: blueTeam);
        return tournament;
    }

    private static void AppendOlympicGroup(
        Tournament tournament,
        string name,
        double weightMax,
        string redTeam = null,
        string blueTeam = null,
        MatchWinTypeEnum winType = MatchWinTypeEnum.PointsWin)
    {
        var group = MakeGroup(name, birthYearMin: 2010, birthYearMax: 2011, weightMax: weightMax);

        for (int i = 0; i < 4; i++)
        {
            var w = MakeWrestler($"W{i + 1:D2}", "Test");
            w.GroupID = group.ID;

            // Alternate teams when both supplied so 2/2 split per team.
            var teamName = i % 2 == 0 ? redTeam : blueTeam;
            if (!string.IsNullOrEmpty(teamName))
            {
                var existingTeam = tournament.TeamApplications.FirstOrDefault(t => t.FullName == teamName);
                if (existingTeam == null)
                {
                    existingTeam = new TeamApplication { ID = Guid.NewGuid(), FullName = teamName, ShortName = teamName };
                    tournament.TeamApplications.Add(existingTeam);
                }
                w.TeamID = existingTeam.ID;
                w.TeamName = teamName;
            }

            w.SeedNumber = i + 1;
            group.Wrestlers.Add(w);
            tournament.Wrestlers.Add(w);
        }

        tournament.Groups.Add(group);

        var processor = new OlympicGroupBracketProcessor();
        processor.Generate(tournament, group);

        // Complete every match so GetResults assigns places 1..4.
        var r1 = group.Bracket.Rounds[0];
        processor.CompleteMatch(r1.RoundMatches[0], isRedWon: true, winType);
        processor.CompleteMatch(r1.RoundMatches[1], isRedWon: true, winType);

        var final = group.Bracket.Rounds[1].RoundMatches[0];
        processor.CompleteMatch(final, isRedWon: true, winType);

        var third = group.Bracket.Rounds.Single(r => r.RoundType == GroupRoundTypeEnum.Additional).RoundMatches[0];
        processor.CompleteMatch(third, isRedWon: true, winType);
    }

    private static List<IAchievementCalculator> RealAchievementCalculators() => new List<IAchievementCalculator>
    {
        new FastestWinAchievementCalculator(),
        new FastestActionAchievementCalculator(),
        new MostAmplitudeActionsAchievementCalculator(),
        new MostPointsCountAchievementCalculator(),
        new MostTusheWinsAchievementCalculator(),
        new MostDominationWinsAchievementCalculator(),
        new WinInLast10SecondsAchievementCalculator()
    };

    private static AgeWeightGroup MakeGroup(string name, int? birthYearMin, int? birthYearMax, double? weightMax)
    {
        return new AgeWeightGroup
        {
            ID = Guid.NewGuid(),
            BirthYearMin = birthYearMin,
            BirthYearMax = birthYearMax,
            WeightMax = weightMax,
            MaxRoundSecond = 180,
            MaxTimeoutSecond = 30,
            MaxActionSecond = 30,
            Wrestlers = new List<Wrestler>()
        };
    }

    private static Wrestler MakeWrestler(string last, string first)
    {
        return new Wrestler
        {
            ID = Guid.NewGuid(),
            LastName = last,
            FirstName = first,
            BirthDate = new DateTime(2010, 1, 1),
            Weight = 60,
            IsEntryFeePaid = true,
            IsWeightApproved = true
        };
    }

    private sealed class StubAchievementCalculator : IAchievementCalculator
    {
        public int InvokedCount { get; private set; }
        public string AchievementTitle => "stub";
        public string AchievementType => "stub";
        public string AchievementDefinition => "stub-def";

        public List<WrestlerAchievement> CalculateAchievement(Tournament tournament, List<TournamentResult> results)
        {
            InvokedCount++;
            return new List<WrestlerAchievement>
            {
                new WrestlerAchievement
                {
                    Title = "stub",
                    AchievementType = "stub",
                    AchievementDefinition = "stub-def",
                    AchievementValue = "ok"
                }
            };
        }
    }
}
