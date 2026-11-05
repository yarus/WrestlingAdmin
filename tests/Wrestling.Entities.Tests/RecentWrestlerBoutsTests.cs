using System;
using FluentAssertions;
using Wrestling.Entities;
using Xunit;

namespace Wrestling.Entities.Tests;

public class RecentWrestlerBoutsTests
{
    private static readonly DateTime Now = new DateTime(2026, 11, 5, 12, 0, 0);

    [Theory]
    [InlineData(0, 2)]
    [InlineData(299, 2)]
    [InlineData(300, 1)]
    [InlineData(599, 1)]
    [InlineData(600, 0)]
    public void Exact_time_boundaries(int seconds, int expected) =>
        RecentWrestlerBouts.WarningLevel(Now.AddSeconds(-seconds), Now).Should().Be(expected);

    private static WrestlingMatch Bout(Wrestler athlete, int minutes) => new WrestlingMatch
    {
        WrestlerInRed = athlete, WrestlerInBlue = new Wrestler { ID = Guid.NewGuid() },
        Status = MatchStatusEnum.Completed, WinType = MatchWinTypeEnum.Tushe,
        StartDateTime = Now.AddMinutes(-30),
        MatchActions = new() { new MatchAction { Type = MatchActionType.MatchCompleted, DateTime = Now.AddMinutes(-minutes) } }
    };

    [Fact]
    public void Uses_latest_completion_including_replacement_history_and_excludes_reverted_results()
    {
        var athlete = new Wrestler { ID = Guid.NewGuid() };
        var older = Bout(athlete, 8);
        var replacement = Bout(new Wrestler { ID = Guid.NewGuid() }, 1);
        replacement.PreviousResults.Add(Bout(athlete, 3));
        var reverted = Bout(athlete, 0);
        reverted.Status = MatchStatusEnum.Pending;
        var last = RecentWrestlerBouts.FindLastCompletions(new[] { older, replacement, reverted }, new[] { athlete.ID });
        last[athlete.ID].Should().Be(Now.AddMinutes(-3));
        last.Should().ContainSingle();
    }

    [Theory]
    [InlineData(MatchWinTypeEnum.FreeWin)]
    [InlineData(MatchWinTypeEnum.NoShow)]
    [InlineData(MatchWinTypeEnum.MutualNoShow)]
    public void Unwrestled_results_do_not_reset_rest(MatchWinTypeEnum type)
    {
        var athlete = new Wrestler { ID = Guid.NewGuid() };
        var real = Bout(athlete, 8);
        var skipped = Bout(athlete, 1);
        skipped.WinType = type;
        RecentWrestlerBouts.FindLastCompletions(new[] { real, skipped }, new[] { athlete.ID })[athlete.ID]
            .Should().Be(Now.AddMinutes(-8));
    }

    [Fact]
    public void Missing_completion_time_does_not_use_start_time()
    {
        var athlete = new Wrestler { ID = Guid.NewGuid() };
        var match = Bout(athlete, 1);
        match.MatchActions.Clear();
        RecentWrestlerBouts.FindLastCompletions(new[] { match }, new[] { athlete.ID }).Should().BeEmpty();
    }
}
