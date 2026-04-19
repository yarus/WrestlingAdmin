using System;
using System.Linq;
using FluentAssertions;
using Wrestling.Entities;
using Wrestling.Entities.Bracket;
using Xunit;

namespace Wrestling.Entities.Tests;

public class WrestlerIdentityTests
{
    [Fact]
    public void SameAs_returns_true_for_reference_equals()
    {
        var w = TestHelpers.MakeWrestler();
        w.SameAs(w).Should().BeTrue();
    }

    [Fact]
    public void SameAs_returns_true_when_IDs_match()
    {
        var id = Guid.NewGuid();
        var a = TestHelpers.MakeWrestler(); a.ID = id;
        var b = TestHelpers.MakeWrestler(); b.ID = id;
        a.SameAs(b).Should().BeTrue();
    }

    [Fact]
    public void SameAs_returns_false_for_different_IDs_and_different_refs()
    {
        var a = TestHelpers.MakeWrestler();
        var b = TestHelpers.MakeWrestler();
        a.SameAs(b).Should().BeFalse();
    }

    [Fact]
    public void SameAs_both_null_is_true_both_null_semantics()
    {
        Wrestler a = null;
        Wrestler b = null;
        a.SameAs(b).Should().BeTrue();
    }

    [Fact]
    public void SameAs_one_null_is_false()
    {
        var w = TestHelpers.MakeWrestler();
        w.SameAs(null).Should().BeFalse();
        ((Wrestler)null).SameAs(w).Should().BeFalse();
    }

    [Fact]
    public void CompleteMatch_propagates_winner_even_when_wrestlers_were_cloned()
    {
        // This is the invariant that protects us if an upstream refactor
        // accidentally clones wrestlers between the bracket and the match state.
        var group = TestHelpers.MakeGroup(4);
        var tournament = TestHelpers.MakeTournament(group);
        var proc = new OlympicGroupBracketProcessor();
        proc.Generate(tournament, group);

        var r1 = group.Bracket.Rounds[0];
        var m1 = r1.RoundMatches[0];

        // Swap the wrestler reference in the match to a clone with the same ID
        var originalWinner = m1.WrestlerInRed;
        var clonedWinner = (Wrestler)originalWinner!.Clone();
        m1.WrestlerInRed = clonedWinner;

        proc.CompleteMatch(m1, isRedWon: true, MatchWinTypeEnum.PointsWin);

        var final = group.Bracket.Rounds[1].RoundMatches[0];
        new[] { final.WrestlerInRed, final.WrestlerInBlue }
            .Any(w => w != null && w.ID == originalWinner.ID)
            .Should().BeTrue("SameAs-based matching tolerates clones with the same ID");
    }
}
