using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using FluentAssertions;
using Wrestling.Entities;
using Wrestling.Providers.Network;
using Xunit;

namespace Wrestling.Providers.Tests;

// PeerStateHasher is the ground truth for convergence detection — two peers
// with the same hash skip pulls; difference triggers a pull. These tests
// pin three properties:
//   1. Determinism: same input → same hash (no time/random/order leakage).
//   2. Sensitivity: every version-bearing field affects the hash.
//   3. Order-independence: groups/matches reordered locally don't change
//      the result (peer A's groups order can differ from peer B's).
public class PeerStateHasherTests
{
    private static Tournament MakeTournament(params (Guid id, int fv, int bv, (string fullNum, int v)[] matches)[] groups)
    {
        var t = new Tournament(new GlobalSettings());
        foreach (var (id, fv, bv, matches) in groups)
        {
            var g = new AgeWeightGroup
            {
                ID = id,
                FieldsVersion = fv,
                BracketVersion = bv,
                Bracket = new GroupBracket
                {
                    Rounds = new List<GroupRound>()
                }
            };
            if (matches != null && matches.Length > 0)
            {
                var round = new GroupRound { RoundNumber = 1, RoundMatches = new List<WrestlingMatch>() };
                foreach (var (fullNum, v) in matches)
                {
                    var parts = fullNum.Split('.');
                    round.RoundMatches.Add(new WrestlingMatch
                    {
                        RoundNumber = int.Parse(parts[0]),
                        BracketNumber = int.Parse(parts[1]),
                        Version = v
                    });
                }
                g.Bracket.Rounds.Add(round);
            }
            t.Groups.Add(g);
        }
        return t;
    }

    [Fact]
    public void Null_tournament_returns_empty_string()
    {
        PeerStateHasher.Compute(null).Should().BeEmpty();
    }

    [Fact]
    public void Empty_tournament_returns_stable_non_empty_hash()
    {
        var t = new Tournament(new GlobalSettings());
        var h1 = PeerStateHasher.Compute(t);
        var h2 = PeerStateHasher.Compute(t);

        h1.Should().NotBeNullOrEmpty();
        h1.Should().Be(h2, "the hash function must be deterministic");
        h1.Length.Should().Be(16, "we truncate to 16 hex characters for compact UDP payload");
    }

    [Fact]
    public void Same_state_produces_same_hash_across_separate_tournament_instances()
    {
        var groupId = Guid.NewGuid();
        var t1 = MakeTournament((groupId, 1, 2, new[] { ("1.1", 1), ("1.2", 0) }));
        var t2 = MakeTournament((groupId, 1, 2, new[] { ("1.1", 1), ("1.2", 0) }));

        PeerStateHasher.Compute(t1).Should().Be(PeerStateHasher.Compute(t2));
    }

    [Fact]
    public void Bumping_FieldsVersion_changes_hash()
    {
        var groupId = Guid.NewGuid();
        var t1 = MakeTournament((groupId, 1, 0, null));
        var t2 = MakeTournament((groupId, 2, 0, null));

        PeerStateHasher.Compute(t1).Should().NotBe(PeerStateHasher.Compute(t2));
    }

    [Fact]
    public void Bumping_BracketVersion_changes_hash()
    {
        var groupId = Guid.NewGuid();
        var t1 = MakeTournament((groupId, 0, 1, null));
        var t2 = MakeTournament((groupId, 0, 2, null));

        PeerStateHasher.Compute(t1).Should().NotBe(PeerStateHasher.Compute(t2));
    }

    [Fact]
    public void Bumping_Match_Version_changes_hash()
    {
        var groupId = Guid.NewGuid();
        var t1 = MakeTournament((groupId, 0, 0, new[] { ("1.1", 0) }));
        var t2 = MakeTournament((groupId, 0, 0, new[] { ("1.1", 1) }));

        PeerStateHasher.Compute(t1).Should().NotBe(PeerStateHasher.Compute(t2));
    }

    [Fact]
    public void Adding_a_group_changes_hash()
    {
        var g1 = Guid.NewGuid();
        var g2 = Guid.NewGuid();
        var t1 = MakeTournament((g1, 0, 0, null));
        var t2 = MakeTournament((g1, 0, 0, null), (g2, 0, 0, null));

        PeerStateHasher.Compute(t1).Should().NotBe(PeerStateHasher.Compute(t2));
    }

    [Fact]
    public void Group_order_does_not_affect_hash()
    {
        // Two peers may have Groups in different orders (drag-reorder, late
        // additions on different machines). The hash must converge regardless.
        var g1 = Guid.NewGuid();
        var g2 = Guid.NewGuid();
        var g3 = Guid.NewGuid();

        var t1 = MakeTournament(
            (g1, 1, 2, null),
            (g2, 3, 4, null),
            (g3, 5, 6, null));

        var t2 = MakeTournament(
            (g3, 5, 6, null),
            (g1, 1, 2, null),
            (g2, 3, 4, null));

        PeerStateHasher.Compute(t1).Should().Be(PeerStateHasher.Compute(t2));
    }

    [Fact]
    public void Match_order_within_group_does_not_affect_hash()
    {
        // A bracket can have matches added/reordered as the bracket processor
        // builds rounds. We hash by BracketFullNumber, so any in-memory order
        // is fine.
        var g = Guid.NewGuid();
        var t1 = MakeTournament((g, 0, 0, new[] { ("1.1", 0), ("1.2", 1), ("2.1", 2) }));
        var t2 = MakeTournament((g, 0, 0, new[] { ("2.1", 2), ("1.1", 0), ("1.2", 1) }));

        PeerStateHasher.Compute(t1).Should().Be(PeerStateHasher.Compute(t2));
    }

    [Fact]
    public void Different_group_id_with_same_versions_produces_different_hash()
    {
        var t1 = MakeTournament((Guid.NewGuid(), 1, 1, null));
        var t2 = MakeTournament((Guid.NewGuid(), 1, 1, null));

        PeerStateHasher.Compute(t1).Should().NotBe(PeerStateHasher.Compute(t2));
    }

    [Fact]
    public void Hash_format_is_lowercase_hex()
    {
        var t = MakeTournament((Guid.NewGuid(), 1, 1, null));
        var hash = PeerStateHasher.Compute(t);

        hash.Should().MatchRegex("^[0-9a-f]{16}$");
    }

    [Fact]
    public void Group_with_null_bracket_does_not_throw()
    {
        // Newly-created groups before bracket generation have Bracket=null.
        var t = new Tournament(new GlobalSettings());
        t.Groups.Add(new AgeWeightGroup { ID = Guid.NewGuid(), FieldsVersion = 1, BracketVersion = 0, Bracket = null });

        Action act = () => PeerStateHasher.Compute(t);
        act.Should().NotThrow();
    }
}
