using System;
using System.Linq;
using FluentAssertions;
using Wrestling.Entities;
using Wrestling.Providers;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Material.Tests.Fakes;
using Wrestling.UI.Material.Tournament.Progress.Schedule;
using Xunit;
using WTournament = Wrestling.Entities.Tournament;

namespace Wrestling.UI.Material.Tests;

public class ScheduleRestWarningTests
{
    private static (ScheduleViewModel Vm, IDataContext Context, ResultsService Results) CreateSchedule()
    {
        var di = TestContainerBuilder.MakeDefault();
        var results = new ResultsService(null, null, null);
        di.Add<IResultsService>(results);
        var context = di.Resolve<IDataContext>();
        var tournament = new WTournament(new GlobalSettings());
        context.Tournament = tournament;
        for (int i = 0; i < 2; i++)
        {
            var mat = new Mat { ID = Guid.NewGuid(), Name = "Mat " + i };
            var group = new AgeWeightGroup { ID = Guid.NewGuid(), Bracket = new GroupBracket() };
            var round = new GroupRound();
            group.Bracket.Rounds.Add(round);
            tournament.Groups.Add(group);
            mat.Groups.Add(group);
            tournament.Mats.Add(mat);
            for (int n = 1; n <= 6; n++)
                round.RoundMatches.Add(new WrestlingMatch
                {
                    GroupID = group.ID, MatchNumber = n,
                    WrestlerInRed = new Wrestler { ID = Guid.NewGuid(), LastName = "Red" + n },
                    WrestlerInBlue = new Wrestler { ID = Guid.NewGuid(), LastName = "Blue" + n }
                });
        }
        var vm = new ScheduleViewModel(di);
        vm.InitData();
        return (vm, context, results);
    }

    [Fact]
    public void Closing_tournament_clears_schedule_before_navigation_without_throwing()
    {
        var (vm, context, results) = CreateSchedule();
        var oldStats = vm.Stats.ToList();
        context.Tournament = null;

        Action closeResults = () => results.Recalculate(null);
        closeResults.Should().NotThrow();
        vm.Stats.Should().BeEmpty();
        vm.SelectedMat.Should().BeNull();
        vm.ScheduleRows.Should().BeEmpty();
        vm.MatsCount.Should().Be(0);
        vm.MatchesCount.Should().Be(0);
        vm.CompletedMatchesCount.Should().Be(0);
        vm.LeftMatchesCount.Should().Be(0);
        oldStats.Should().OnlyContain(stat => stat.Matches == null);
        vm.FilterString = "Red";
        vm.ScheduleRows.Should().BeEmpty();
        vm.NotifyNavigatingOut();
    }

    [Fact]
    public void Rows_track_completion_revert_participant_changes_and_source_clear_without_recalculation()
    {
        var (vm, _, _) = CreateSchedule();
        var stat = vm.SelectedMat;
        var match = stat.Matches[0];
        match.Status = MatchStatusEnum.Completed;
        vm.ScheduleRows.Select(row => row.Match).Should().Equal(stat.MatchesReady);
        vm.ScheduleRows.Should().HaveCount(5);

        match.Status = MatchStatusEnum.Pending;
        vm.ScheduleRows.Should().HaveCount(6);
        var blue = match.WrestlerInBlue;
        match.WrestlerInBlue = null;
        vm.ScheduleRows.Should().HaveCount(5);
        match.WrestlerInBlue = blue;
        vm.ScheduleRows.Select(row => row.Match).Should().Equal(stat.MatchesReady);

        var replacement = new WrestlingMatch { WrestlerInRed = match.WrestlerInRed, WrestlerInBlue = blue };
        stat.Matches[0] = replacement;
        vm.ScheduleRows.First().Match.Should().BeSameAs(replacement, "membership can change without changing the ready count");

        stat.Matches = null;
        vm.ScheduleRows.Should().BeEmpty();
        vm.NotifyNavigatingOut();
    }

    [Fact]
    public void Ready_queue_changes_promote_sixth_match_into_warning_window()
    {
        var (vm, context, _) = CreateSchedule();
        var matches = vm.SelectedMat.Matches;
        var sixth = matches[5];
        var history = new WrestlingMatch
        {
            WrestlerInRed = sixth.WrestlerInRed, Status = MatchStatusEnum.Completed,
            WinType = MatchWinTypeEnum.PointsWin,
            MatchActions = new() { new MatchAction { Type = MatchActionType.MatchCompleted, DateTime = DateTime.Now.AddMinutes(-2) } }
        };
        context.Tournament.Groups[0].Bracket.Rounds[0].RoundMatches.Add(history);
        vm.SelectedMat = vm.SelectedMat;
        vm.ScheduleRows.Last().HasRestWarning.Should().BeFalse();

        matches[0].Status = MatchStatusEnum.Completed;
        vm.ScheduleRows.Last().Match.Should().BeSameAs(sixth);
        vm.ScheduleRows.Last().RestWarningLevel.Should().Be(2);
        vm.NotifyNavigatingOut();
    }

    [Fact]
    public void Queue_subscription_follows_selection_navigation_and_mat_filter()
    {
        var (vm, _, results) = CreateSchedule();
        var first = vm.SelectedMat;
        var second = vm.Stats[1];
        vm.SelectedMat = second;
        var rows = vm.ScheduleRows;
        first.Matches[0].Status = MatchStatusEnum.Completed;
        vm.ScheduleRows.Should().BeSameAs(rows, "the old mat must be unsubscribed");
        second.Matches[0].Status = MatchStatusEnum.Completed;
        vm.ScheduleRows.Should().HaveCount(5);
        var secondMatches = second.Matches.ToList();
        vm.NotifyNavigatingOut();
        rows = vm.ScheduleRows;
        results.Recalculate(null);
        vm.ScheduleRows.Should().BeSameAs(rows, "the inactive screen must unsubscribe from results");
        vm.SelectedMat = second;
        vm.ScheduleRows.Should().BeEmpty();
        rows = vm.ScheduleRows;
        second.Matches = new System.Collections.ObjectModel.ObservableCollection<WrestlingMatch>(secondMatches);
        vm.ScheduleRows.Should().BeSameAs(rows, "inactive selection must not resubscribe");

        vm.InitData();
        vm.SelectedMat.MatID.Should().Be(second.MatID);
        vm.ScheduleRows.Should().HaveCount(5);
        var beforeFilter = vm.SelectedMat;
        vm.MatIdFilter = first.MatID;
        vm.SelectedMat.MatID.Should().Be(first.MatID);
        beforeFilter.Matches.Should().BeNull();
        vm.ScheduleRows.Should().HaveCount(5);
        vm.NotifyNavigatingOut();
    }

    [Fact]
    public void Warning_is_limited_to_canonical_first_five_and_survives_filtering_and_reentry()
    {
        var di = TestContainerBuilder.MakeDefault();
        var results = new ResultsService(null, null, null);
        di.Add<IResultsService>(results);
        var tournament = new WTournament(new GlobalSettings());
        di.Resolve<IDataContext>().Tournament = tournament;
        var mat = new Mat { ID = Guid.NewGuid(), Name = "A" };
        tournament.Mats.Add(mat);
        var group = new AgeWeightGroup { ID = Guid.NewGuid(), Bracket = new GroupBracket() };
        tournament.Groups.Add(group);
        mat.Groups.Add(group);
        var athlete = new Wrestler { ID = Guid.NewGuid(), LastName = "Recent" };
        var round = new GroupRound();
        group.Bracket.Rounds.Add(round);
        for (int i = 1; i <= 6; i++)
            round.RoundMatches.Add(new WrestlingMatch
            {
                GroupID = group.ID, MatchNumber = i, WrestlerInRed = athlete,
                WrestlerInBlue = new Wrestler { ID = Guid.NewGuid(), LastName = "Opponent" + i }
            });
        var completed = new WrestlingMatch
        {
            GroupID = group.ID, MatchNumber = 0, WrestlerInRed = athlete,
            WrestlerInBlue = new Wrestler { ID = Guid.NewGuid() }, Status = MatchStatusEnum.Completed,
            WinType = MatchWinTypeEnum.PointsWin,
            MatchActions = new() { new MatchAction { Type = MatchActionType.MatchCompleted, DateTime = DateTime.Now.AddMinutes(-2) } }
        };
        round.RoundMatches.Add(completed);
        var vm = new ScheduleViewModel(di);
        vm.InitData();
        vm.ScheduleRows.Should().HaveCount(6);
        vm.ScheduleRows.Take(5).Should().OnlyContain(r => r.RestWarningLevel == 2);
        vm.ScheduleRows.Last().HasRestWarning.Should().BeFalse();
        vm.OpenMatchCommand.CanExecute(vm.ScheduleRows[0]).Should().BeTrue();

        vm.FilterString = "Opponent6";
        vm.ScheduleRows.Should().ContainSingle().Which.HasRestWarning.Should().BeFalse();
        vm.FilterString = "Opponent1";
        vm.ScheduleRows.Should().ContainSingle().Which.HasRestWarning.Should().BeTrue();

        completed.Status = MatchStatusEnum.Pending;
        // A reverted bout still has its historical actions, but cannot serve
        // as the last accepted completion when entering the page again.
        results.Recalculate(null);
        vm.ScheduleRows.Should().ContainSingle().Which.HasRestWarning.Should().BeFalse();
        vm.NotifyNavigatingOut();
        completed.Status = MatchStatusEnum.Completed;
        results.Recalculate(null);
        vm.ScheduleRows.Should().ContainSingle().Which.HasRestWarning.Should().BeFalse("the inactive screen must unsubscribe");
        vm.InitData();
        vm.ScheduleRows.Should().ContainSingle().Which.HasRestWarning.Should().BeTrue();
        vm.NotifyNavigatingOut();
    }

    [Fact]
    public void Both_athletes_are_named_and_most_recent_completion_sets_color()
    {
        var red = new Wrestler { ID = Guid.NewGuid(), LastName = "Red" };
        var blue = new Wrestler { ID = Guid.NewGuid(), LastName = "Blue" };
        var match = new WrestlingMatch { WrestlerInRed = red, WrestlerInBlue = blue };
        var now = DateTime.Now;
        var row = new ScheduleMatchRow(match, new System.Collections.Generic.Dictionary<Guid, DateTime>
        {
            [red.ID] = now.AddMinutes(-7), [blue.ID] = now.AddMinutes(-2)
        }, now, true);
        row.RestWarningLevel.Should().Be(2);
        row.RestWarningText.Should().Contain("Red").And.Contain("Blue");
        new ScheduleMatchRow(match, new System.Collections.Generic.Dictionary<Guid, DateTime>(), now, true)
            .HasRestWarning.Should().BeFalse();
    }
}
