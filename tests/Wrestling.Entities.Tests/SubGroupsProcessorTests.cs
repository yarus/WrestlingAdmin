using System.Linq;
using FluentAssertions;
using Wrestling.Entities.Bracket;
using Xunit;

namespace Wrestling.Entities.Tests;

// Coverage for SubGroupsToOlympicBracketProcessor (6-8 wrestlers).
// Layout:
//   Main: round-robin in two subgroups (split by seed: A=top half, B=rest).
//   Additional: SF (2 matches, gold A × silver B / gold B × silver A) → F + 3rd place.
public class SubGroupsProcessorTests
{
    private static (Tournament, AgeWeightGroup, SubGroupsToOlympicBracketProcessor) Setup(int wrestlers)
    {
        var group = TestHelpers.MakeGroup(wrestlers);
        var t = TestHelpers.MakeTournament(group);
        var proc = new SubGroupsToOlympicBracketProcessor();
        proc.Generate(t, group);
        return (t, group, proc);
    }

    [Fact]
    public void Generate_6_wrestlers_creates_3_main_rounds_for_subgroup_round_robin()
    {
        // 3+3 wrestlers each side, round-robin = 3 rounds (each pair plays once).
        var (_, g, _) = Setup(6);

        var main = g.Bracket.Rounds.Where(r => r.RoundType == GroupRoundTypeEnum.Main).ToList();
        main.Should().HaveCount(3);
        main.Sum(r => r.RoundMatches.Count).Should().Be(6); // 2 subgroups × 3 matches
    }

    [Fact]
    public void Generate_creates_three_additional_rounds_for_SF_F_third_place()
    {
        var (_, g, _) = Setup(6);
        var add = g.Bracket.Rounds.Where(r => r.RoundType == GroupRoundTypeEnum.Additional).ToList();
        add.Should().HaveCount(3); // SF (2 matches) + Final (1) + 3rd place (1)

        add[0].RoundMatches.Should().HaveCount(2);
        add[1].RoundMatches.Should().HaveCount(1); // Final
        add[2].RoundMatches.Should().HaveCount(1); // 3rd place
    }

    [Fact]
    public void Generate_7_wrestlers_uses_4_3_split()
    {
        // 7 wrestlers → A has 4 (round-robin = 6 matches), B has 3 (= 3 matches).
        // Total main matches: 9.
        var (_, g, _) = Setup(7);
        var main = g.Bracket.Rounds.Where(r => r.RoundType == GroupRoundTypeEnum.Main).ToList();
        main.Sum(r => r.RoundMatches.Count).Should().Be(9);
    }

    [Fact]
    public void Final_match_is_pending_until_all_subgroup_matches_completed()
    {
        var (_, g, _) = Setup(6);
        var addRounds = g.Bracket.Rounds.Where(r => r.RoundType == GroupRoundTypeEnum.Additional).ToList();
        // Semi-finals are addRounds[0]
        addRounds[0].RoundMatches.Should().OnlyContain(m =>
            m.WrestlerInRed == null && m.WrestlerInBlue == null && m.Status == MatchStatusEnum.Pending);
    }

    [Fact]
    public void Subgroup_completion_promotes_top_two_per_side_to_semifinals()
    {
        var (_, g, proc) = Setup(6);

        // Drive all main matches to a deterministic outcome (red wins).
        foreach (var round in g.Bracket.Rounds.Where(r => r.RoundType == GroupRoundTypeEnum.Main).ToList())
            foreach (var m in round.RoundMatches.ToList())
                if (m.Status == MatchStatusEnum.Pending && m.WrestlerInRed != null && m.WrestlerInBlue != null)
                    proc.CompleteMatch(m, true, MatchWinTypeEnum.PointsWin);

        var addRounds = g.Bracket.Rounds.Where(r => r.RoundType == GroupRoundTypeEnum.Additional).ToList();
        var sf = addRounds[0];

        // Both SF matches now have all 4 wrestlers populated.
        sf.RoundMatches.Should().OnlyContain(m =>
            m.WrestlerInRed != null && m.WrestlerInBlue != null);

        // Each SF gets one wrestler from A and one from B (cross-wire).
        var allSemiFinalists = sf.RoundMatches.SelectMany(m => new[] { m.WrestlerInRed, m.WrestlerInBlue }).ToList();
        allSemiFinalists.Distinct().Should().HaveCount(4);
    }

    [Fact]
    public void SF_completion_promotes_winners_to_Final_and_losers_to_third_place()
    {
        var (_, g, proc) = Setup(6);
        // Complete subgroups
        foreach (var round in g.Bracket.Rounds.Where(r => r.RoundType == GroupRoundTypeEnum.Main).ToList())
            foreach (var m in round.RoundMatches.ToList())
                if (m.Status == MatchStatusEnum.Pending && m.WrestlerInRed != null && m.WrestlerInBlue != null)
                    proc.CompleteMatch(m, true, MatchWinTypeEnum.PointsWin);

        var addRounds = g.Bracket.Rounds.Where(r => r.RoundType == GroupRoundTypeEnum.Additional).ToList();
        var sfRound = addRounds[0];

        var sf1 = sfRound.RoundMatches[0];
        var sf2 = sfRound.RoundMatches[1];
        var sf1Winner = sf1.WrestlerInRed;
        var sf1Loser = sf1.WrestlerInBlue;
        var sf2Winner = sf2.WrestlerInRed;
        var sf2Loser = sf2.WrestlerInBlue;

        proc.CompleteMatch(sf1, true, MatchWinTypeEnum.PointsWin);
        proc.CompleteMatch(sf2, true, MatchWinTypeEnum.PointsWin);

        var finalMatch = addRounds[1].RoundMatches[0];
        var thirdPlace = addRounds[2].RoundMatches[0];

        new[] { finalMatch.WrestlerInRed, finalMatch.WrestlerInBlue }
            .Should().BeEquivalentTo(new[] { sf1Winner, sf2Winner });
        new[] { thirdPlace.WrestlerInRed, thirdPlace.WrestlerInBlue }
            .Should().BeEquivalentTo(new[] { sf1Loser, sf2Loser });
    }

    [Fact]
    public void Full_lifecycle_assigns_FinalPlace_for_top_4_via_final_and_third_place()
    {
        var (_, g, proc) = Setup(6);

        foreach (var round in g.Bracket.Rounds.Where(r => r.RoundType == GroupRoundTypeEnum.Main).ToList())
            foreach (var m in round.RoundMatches.ToList())
                if (m.Status == MatchStatusEnum.Pending && m.WrestlerInRed != null && m.WrestlerInBlue != null)
                    proc.CompleteMatch(m, true, MatchWinTypeEnum.PointsWin);

        var addRounds = g.Bracket.Rounds.Where(r => r.RoundType == GroupRoundTypeEnum.Additional).ToList();
        // Complete SFs
        foreach (var sf in addRounds[0].RoundMatches.ToList())
            proc.CompleteMatch(sf, true, MatchWinTypeEnum.PointsWin);
        // Final
        var finalMatch = addRounds[1].RoundMatches[0];
        var goldExpected = finalMatch.WrestlerInRed;
        var silverExpected = finalMatch.WrestlerInBlue;
        proc.CompleteMatch(finalMatch, true, MatchWinTypeEnum.PointsWin);
        // 3rd
        var thirdMatch = addRounds[2].RoundMatches[0];
        var bronzeExpected = thirdMatch.WrestlerInRed;
        var fourthExpected = thirdMatch.WrestlerInBlue;
        proc.CompleteMatch(thirdMatch, true, MatchWinTypeEnum.PointsWin);

        proc.GetResults();

        goldExpected!.FinalPlace.Should().Be(1);
        silverExpected!.FinalPlace.Should().Be(2);
        bronzeExpected!.FinalPlace.Should().Be(3);
        fourthExpected!.FinalPlace.Should().Be(4);
        // Other 2 wrestlers fall through to classification-points ordering.
        g.Wrestlers.Where(w => w.FinalPlace > 4).Should().HaveCount(2);
    }

    [Fact]
    public void Revert_subgroup_match_blocks_when_SF_already_completed()
    {
        var (_, g, proc) = Setup(6);

        var mainRounds = g.Bracket.Rounds.Where(r => r.RoundType == GroupRoundTypeEnum.Main).ToList();
        foreach (var round in mainRounds)
            foreach (var m in round.RoundMatches.ToList())
                if (m.Status == MatchStatusEnum.Pending && m.WrestlerInRed != null && m.WrestlerInBlue != null)
                    proc.CompleteMatch(m, true, MatchWinTypeEnum.PointsWin);

        // Complete a semifinal — only then is main-match revert blocked.
        var sfRound = g.Bracket.Rounds.First(r => r.RoundType == GroupRoundTypeEnum.Additional);
        proc.CompleteMatch(sfRound.RoundMatches[0], true, MatchWinTypeEnum.PointsWin);

        var anyMain = mainRounds.SelectMany(r => r.RoundMatches).First(m => m.Status == MatchStatusEnum.Completed);
        proc.CanMatchBeReverted(anyMain).Should().BeFalse();
    }

    [Fact]
    public void GetSemiFinalRound_and_GetFinalRound_return_expected_rounds()
    {
        var (_, g, proc) = Setup(6);
        var sf = proc.GetSemiFinalRound(g);
        var f = proc.GetFinalRound(g);

        sf.Should().NotBeNull();
        sf!.RoundMatches.Should().HaveCount(2);
        f.Should().NotBeNull();
        f!.RoundMatches.Should().HaveCount(1);
        sf.RoundNumber.Should().BeLessThan(f.RoundNumber);
    }

    // UWW: when both finalists are mutually DSQ'd, the bronze match decides
    // 1-2; everyone else shifts up by 2. (Single-bronze adaptation of the
    // OlympicConsilationFinalists rule.)
    [Fact]
    public void Final_mutual_DSQ_promotes_bronze_winner_to_1st_loser_to_2nd()
    {
        var (_, g, proc) = Setup(6);

        foreach (var round in g.Bracket.Rounds.Where(r => r.RoundType == GroupRoundTypeEnum.Main).ToList())
            foreach (var m in round.RoundMatches.ToList())
                if (m.Status == MatchStatusEnum.Pending && m.WrestlerInRed != null && m.WrestlerInBlue != null)
                    proc.CompleteMatch(m, true, MatchWinTypeEnum.PointsWin);

        var addRounds = g.Bracket.Rounds.Where(r => r.RoundType == GroupRoundTypeEnum.Additional).ToList();
        foreach (var sf in addRounds[0].RoundMatches.ToList())
            proc.CompleteMatch(sf, true, MatchWinTypeEnum.PointsWin);

        var finalMatch = addRounds[1].RoundMatches[0];
        var origFinalRed = finalMatch.WrestlerInRed;
        var origFinalBlue = finalMatch.WrestlerInBlue;
        proc.CompleteMatch(finalMatch, null, MatchWinTypeEnum.MutualDisqualify);

        var thirdMatch = addRounds[2].RoundMatches[0];
        var bronzeWinner = thirdMatch.WrestlerInRed;
        var bronzeLoser = thirdMatch.WrestlerInBlue;
        proc.CompleteMatch(thirdMatch, true, MatchWinTypeEnum.PointsWin);

        proc.GetResults();

        bronzeWinner!.FinalPlace.Should().Be(1, because: "bronze winner promoted by UWW final-DSQ rule");
        bronzeLoser!.FinalPlace.Should().Be(2, because: "bronze loser promoted to silver");
        origFinalRed!.IsDisqualified.Should().BeTrue();
        origFinalBlue!.IsDisqualified.Should().BeTrue();
        origFinalRed.FinalPlace.Should().BeNull(because: "DSQ'd wrestlers stay placeless");
        origFinalBlue.FinalPlace.Should().BeNull();
        // Remaining 2 wrestlers (not finalists, not bronze) take 3-4 by classification.
        var others = g.Wrestlers.Where(w => w != bronzeWinner && w != bronzeLoser
                                            && w != origFinalRed && w != origFinalBlue).ToList();
        others.Should().HaveCount(2);
        others.Select(w => w.FinalPlace).Should().BeEquivalentTo(new int?[] { 3, 4 });
    }

    [Fact]
    public void Revert_SF_clears_final_and_third_place_slots()
    {
        var (_, g, proc) = Setup(6);

        foreach (var round in g.Bracket.Rounds.Where(r => r.RoundType == GroupRoundTypeEnum.Main).ToList())
            foreach (var m in round.RoundMatches.ToList())
                if (m.Status == MatchStatusEnum.Pending && m.WrestlerInRed != null && m.WrestlerInBlue != null)
                    proc.CompleteMatch(m, true, MatchWinTypeEnum.PointsWin);

        var addRounds = g.Bracket.Rounds.Where(r => r.RoundType == GroupRoundTypeEnum.Additional).ToList();
        var sf1 = addRounds[0].RoundMatches[0];
        proc.CompleteMatch(sf1, true, MatchWinTypeEnum.PointsWin);

        proc.CanMatchBeReverted(sf1).Should().BeTrue();
        proc.RevertMatch(sf1);

        var finalMatch = addRounds[1].RoundMatches[0];
        var thirdMatch = addRounds[2].RoundMatches[0];

        // Slots populated by sf1 wrestlers should be cleared.
        new[] { finalMatch.WrestlerInRed, finalMatch.WrestlerInBlue }
            .Should().NotContain(sf1.WrestlerInRed);
        new[] { thirdMatch.WrestlerInRed, thirdMatch.WrestlerInBlue }
            .Should().NotContain(sf1.WrestlerInBlue);
    }
}
