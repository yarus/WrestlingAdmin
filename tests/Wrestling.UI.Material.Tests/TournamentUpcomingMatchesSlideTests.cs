using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using FluentAssertions;
using Newtonsoft.Json;
using Wrestling.Entities;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Material.Slider.Slides.TournamentUpcomingMatchesSlide;
using Wrestling.UI.Material.Tests.Fakes;
using Xunit;
using WTournament = Wrestling.Entities.Tournament;

namespace Wrestling.UI.Material.Tests;

public sealed class TournamentUpcomingMatchesSlideTests
{
    [Theory]
    [InlineData(1, 8, 18)]
    [InlineData(2, 8, 18)]
    [InlineData(3, 8, 18)]
    [InlineData(4, 8, 16)]
    [InlineData(2, 6, 24)]
    public async Task Cards_remain_readable_and_centered_without_overlapping(int matCount, int matchCount, double minimumTextHeight)
    {
        var completion = new TaskCompletionSource();
        var thread = new Thread(() =>
        {
            try
            {
                var di = TestContainerBuilder.MakeDefault();
                var tournament = new WTournament(new GlobalSettings());
                di.Resolve<IDataContext>().Tournament = tournament;
                for (var i = 0; i < matCount; i++)
                {
                    var mat = new Mat { ID = Guid.NewGuid(), Name = "Mat " + i };
                    mat.Groups.Add(Group(null, Enumerable.Range(1, matchCount).Select(number =>
                    {
                        var match = ReadyMatch(number);
                        match.GroupName = "2018–2019 гг. 30 кг. М.";
                        match.RoundName = "1/4 финала";
                        match.WrestlerInRed.LastName = "Абдурахманов";
                        match.WrestlerInRed.FirstName = "Александр";
                        match.WrestlerInRed.TeamName = "Красноярский край";
                        return match;
                    }).ToArray()));
                    tournament.Mats.Add(mat);
                }
                var slide = new ScreenSlide(); slide.SetNamedValue("ShowMatchesCount", matchCount);
                var vm = new TournamentUpcomingMatchesViewModel(di); vm.InitContext(slide);
                var view = new TournamentUpcomingMatchesView { DataContext = vm };
                view.Measure(new Size(1920, 1080)); view.Arrange(new Rect(0, 0, 1920, 1080)); view.UpdateLayout();
                var names = Descendants(view).OfType<TextBlock>().Where(t => t.Text == "АБДУРАХМАНОВ АЛЕКСАНДР").ToList();
                names.Should().HaveCount(matCount * matchCount);
                foreach (var name in names)
                {
                    var bounds = name.TransformToAncestor(view).TransformBounds(new Rect(name.RenderSize));
                    bounds.Height.Should().BeGreaterThan(minimumTextHeight, "participant text must remain readable at 1080p");
                    bounds.Left.Should().BeGreaterThanOrEqualTo(0);
                    bounds.Right.Should().BeLessThanOrEqualTo(1920);
                }
                var cards = Descendants(view).OfType<Border>().Where(b => b.CornerRadius.TopLeft == 10)
                    .Select(b => b.TransformToAncestor(view).TransformBounds(new Rect(b.RenderSize))).ToList();
                cards.Should().HaveCount(matCount * matchCount);
                for (var i = 0; i < cards.Count; i++)
                {
                    var column = i / matchCount;
                    var center = 30 + (column + 0.5) * 1860 / matCount;
                    (cards[i].Left + cards[i].Width / 2).Should().BeApproximately(center, 0.1);
                    cards[i].Width.Should().BeLessThanOrEqualTo(1860.0 / matCount - 20 + 0.1);
                    cards[i].Bottom.Should().BeLessThanOrEqualTo(1056.1);
                    if (i % matchCount != 0) cards[i].Top.Should().BeGreaterThan(cards[i - 1].Bottom);
                }
                completion.SetResult();
            }
            catch (Exception ex) { completion.SetException(ex); }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        await completion.Task;
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    [Fact]
    public void Selected_mats_have_independent_ordered_queues_for_their_active_parts()
    {
        var di = TestContainerBuilder.MakeDefault();
        var tournament = new WTournament(new GlobalSettings());
        di.Resolve<IDataContext>().Tournament = tournament;
        var a = new Mat { ID = Guid.NewGuid(), Name = "A", ActivePartID = Guid.NewGuid() };
        var b = new Mat { ID = Guid.NewGuid(), Name = "B", ActivePartID = Guid.NewGuid() };
        var c = new Mat { ID = Guid.NewGuid(), Name = "C" };
        foreach (var mat in new[] { a, b, c }) tournament.Mats.Add(mat);
        var first = ReadyMatch(1);
        var second = ReadyMatch(2);
        var completed = ReadyMatch(0);
        completed.Status = MatchStatusEnum.Completed;
        a.Groups.Add(Group(a.ActivePartID, second, completed, first));
        a.Groups.Add(Group(Guid.NewGuid(), ReadyMatch(0)));
        a.Groups.Add(new AgeWeightGroup());
        b.Groups.Add(Group(b.ActivePartID, ReadyMatch(3)));
        var slide = new ScreenSlide();
        slide.SetNamedValue("MatIDs", $"{c.ID},{a.ID}");
        slide.SetNamedValue("ShowMatchesCount", 1);
        var vm = new TournamentUpcomingMatchesViewModel(di);

        vm.InitContext(slide);

        vm.Columns.Select(x => x.Mat).Should().Equal(a, c);
        vm.Columns[0].Matches.Should().Equal(first);
        vm.Columns[1].IsEmpty.Should().BeTrue();
        first.Status = MatchStatusEnum.Completed;
        vm.TimerTick();
        vm.Columns[0].Matches.Should().Equal(second);
        a.ActivePartID = Guid.NewGuid();
        vm.TimerTick();
        vm.Columns[0].IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void Settings_round_trip_and_reentry_do_not_leak_selection_between_slides()
    {
        var di = TestContainerBuilder.MakeDefault();
        var tournament = new WTournament(new GlobalSettings());
        di.Resolve<IDataContext>().Tournament = tournament;
        foreach (var name in new[] { "A", "B", "C" }) tournament.Mats.Add(new Mat { ID = Guid.NewGuid(), Name = name });
        var type = new TournamentUpcomingMatchesSlide(di);
        var settings = (TournamentUpcomingMatchesSettingsViewModel)type.SettingsControl;
        var slide = new ScreenSlide { SlideType = type.SlideType };
        settings.InitContext(slide);
        settings.Mats[1].IsSelected = false;
        settings.Mats[2].IsSelected = false;
        settings.Mats[0].IsSelected = false;
        settings.Mats[0].IsSelected.Should().BeTrue("at least one mat must remain selected");
        settings.ShowMatchesCount = 6;
        slide = JsonConvert.DeserializeObject<ScreenSlide>(JsonConvert.SerializeObject(slide));

        settings.InitContext(new ScreenSlide());
        settings.Mats.Should().OnlyContain(m => m.IsSelected);
        settings.InitContext(slide);
        settings.Mats.Select(m => m.IsSelected).Should().Equal(true, false, false);
        settings.ShowMatchesCount.Should().Be(6);
        var vm = new TournamentUpcomingMatchesViewModel(di);
        vm.InitContext(slide);
        vm.Columns.Select(c => c.Mat).Should().Equal(tournament.Mats[0]);
        settings.SelectAllCommand.Execute(null);
        vm.InitContext(slide);
        vm.Columns.Should().HaveCount(3);
    }

    [Fact]
    public void Removed_selected_mat_never_falls_back_to_showing_other_mats()
    {
        var di = TestContainerBuilder.MakeDefault();
        var tournament = new WTournament(new GlobalSettings());
        di.Resolve<IDataContext>().Tournament = tournament;
        tournament.Mats.Add(new Mat { ID = Guid.NewGuid(), Name = "A" });
        var slide = new ScreenSlide();
        slide.SetNamedValue("MatIDs", Guid.NewGuid().ToString());
        var vm = new TournamentUpcomingMatchesViewModel(di);
        vm.InitContext(slide);
        vm.HasNoColumns.Should().BeTrue();
        vm.InitContext(new ScreenSlide());
        vm.Columns.Should().HaveCount(1);
        di.Resolve<IDataContext>().Tournament = null;
        vm.TimerTick();
        vm.HasNoColumns.Should().BeTrue();
    }

    [Fact]
    public void Each_host_has_its_own_state()
    {
        var di = TestContainerBuilder.MakeDefault();
        di.Resolve<IDataContext>().Tournament = new WTournament(new GlobalSettings());
        var type = new TournamentUpcomingMatchesSlide(di);
        var first = (TournamentUpcomingMatchesViewModel)type.CreateViewControl();
        var second = (TournamentUpcomingMatchesViewModel)type.CreateViewControl();
        var one = new ScreenSlide(); one.SetNamedValue("ShowMatchesCount", 1);
        var six = new ScreenSlide(); six.SetNamedValue("ShowMatchesCount", 6);
        first.InitContext(one); second.InitContext(six);
        first.ShowMatchesCount.Should().Be(1);
        second.ShowMatchesCount.Should().Be(6);
    }

    private static WrestlingMatch ReadyMatch(int number) => new WrestlingMatch
    { MatchNumber = number, WrestlerInRed = new Wrestler(), WrestlerInBlue = new Wrestler() };
    private static AgeWeightGroup Group(Guid? part, params WrestlingMatch[] matches)
    {
        var group = new AgeWeightGroup { PartID = part, Bracket = new GroupBracket() };
        group.Bracket.Rounds.Add(new GroupRound { RoundMatches = new List<WrestlingMatch>(matches) });
        return group;
    }
}
