using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Wrestling.Entities;
using Wrestling.Entities.Bracket;
using Xunit;

namespace Wrestling.Entities.Tests;

public class RestAwareScheduleTests
{
    private static AgeWeightGroup Circle(int count)
    {
        var group = new AgeWeightGroup { ID = Guid.NewGuid() };
        group.Wrestlers = Enumerable.Range(0, count).Select(i => new Wrestler
        {
            ID = Guid.NewGuid(), GroupID = group.ID, SeedNumber = i + 1
        }).ToList();
        new RoundRobinGroupBracketProcessor().Generate(new Tournament(new GlobalSettings()), group);
        return group;
    }

    private static List<WrestlingMatch> Matches(AgeWeightGroup group) =>
        group.Bracket.Rounds.SelectMany(r => r.RoundMatches).ToList();

    [Fact]
    public void Four_circles_allow_three_bouts_rest_and_preserve_each_category_order()
    {
        var groups = Enumerable.Range(0, 4).Select(_ => Circle(3)).ToList();
        var original = groups.SelectMany(Matches).ToList();
        var ordered = RestAwareMatchOrder.Optimize(original, groups);
        var last = new Dictionary<Guid, int>();
        for (int i = 0; i < ordered.Count; i++)
        {
            foreach (var athlete in new[] { ordered[i].WrestlerInRed, ordered[i].WrestlerInBlue })
            {
                if (last.TryGetValue(athlete.ID, out int previous)) (i - previous - 1).Should().BeGreaterThanOrEqualTo(3);
                last[athlete.ID] = i;
            }
        }
        foreach (var group in groups)
            ordered.Where(m => m.GroupID == group.ID).Should().Equal(Matches(group));
        RestAwareMatchOrder.Optimize(original, groups).Should().Equal(ordered);
    }

    [Fact]
    public void One_category_is_unchanged_when_no_other_bouts_exist()
    {
        var group = Circle(3);
        RestAwareMatchOrder.Optimize(Matches(group), new[] { group }).Should().Equal(Matches(group));
    }

    [Fact]
    public void Final_and_bronze_do_not_falsely_require_rest_between_them()
    {
        var group = Olympic(8);
        var other = Circle(2);
        var bronze = group.Bracket.Rounds.Last().RoundMatches.ToList();
        var final = group.Bracket.MainRounds().Last().RoundMatches.Single();
        var original = bronze.Concat(new[] { final }).Concat(Matches(other)).ToList();
        RestAwareMatchOrder.Optimize(original, new[] { group, other }).Should().Equal(original);
    }

    [Fact]
    public void Structural_automatic_consolation_passes_are_not_counted_as_rest()
    {
        var group = Olympic(9);
        var circles = Enumerable.Range(0, 3).Select(_ => Circle(3)).ToList();
        var filler = Circle(2);
        var first = Matches(circles[0]);
        // The upper side's first consolation cell has only one possible loser
        // on the shortest finalist path (the first round is a bye).
        var automatic = group.Bracket.AdditionalRounds().First().RoundMatches.First();
        var original = new[] { first[0], automatic }
            .Concat(Matches(circles[1]).Take(1)).Concat(Matches(circles[2]).Take(1))
            .Concat(first.Skip(1)).Concat(Matches(filler)).Concat(Matches(circles[1]).Skip(1)).Concat(Matches(circles[2]).Skip(1)).ToList();
        var ordered = RestAwareMatchOrder.Optimize(original, circles.Append(group).Append(filler));
        // Two genuine intervening bouts, plus the automatic cell, must not
        // qualify as three bouts of rest.
        ordered.Take(5).Should().NotContain(first[1]);
    }

    [Fact]
    public void Optional_consolation_bout_still_requires_rest_before_bronze()
    {
        var group = Olympic(9);
        var branch = group.Bracket.AdditionalRounds().Select(r => r.RoundMatches[1]).ToList();
        var fillers = Enumerable.Range(0, 3).Select(_ => Circle(2)).ToList();
        var original = new[] { branch[0] }.Concat(fillers.Take(2).SelectMany(Matches))
            .Concat(new[] { branch[1] }).Concat(Matches(fillers[2])).ToList();
        var ordered = RestAwareMatchOrder.Optimize(original, fillers.Append(group));
        ordered.Last().Should().BeSameAs(branch[1]);
    }

    private static AgeWeightGroup Olympic(int count)
    {
        var group = new AgeWeightGroup { ID = Guid.NewGuid() };
        group.Wrestlers = Enumerable.Range(0, count).Select(i => new Wrestler
        {
            ID = Guid.NewGuid(), GroupID = group.ID, SeedNumber = i + 1
        }).ToList();
        new OlympicWithConsolationFromFinalistsGroupBracketProcessor().Generate(new Tournament(new GlobalSettings()), group);
        return group;
    }
}
