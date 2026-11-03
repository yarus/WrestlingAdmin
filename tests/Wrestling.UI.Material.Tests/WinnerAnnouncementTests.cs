using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using FluentAssertions;
using Wrestling.Entities;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Material.ScoreScreen;
using Wrestling.UI.Material.Tests.Fakes;
using Xunit;
using WTournament = Wrestling.Entities.Tournament;

namespace Wrestling.UI.Material.Tests;

public sealed class WinnerAnnouncementTests
{
    private static (ScoreScreenViewModel Vm, DataContext Context, TeamApplication Team) Build()
    {
        var di = TestContainerBuilder.MakeDefault();
        var tournament = new WTournament(new GlobalSettings());
        var team = new TeamApplication
        {
            ID = Guid.NewGuid(), ShortName = "СШОР имени Ивана Ярыгина", FullName = "Спортивная школа",
            City = "Комсомольск-на-Амуре", EmblemPath = "DefaultLogo.png"
        };
        tournament.TeamApplications.Add(team);
        var group = new AgeWeightGroup { ID = Guid.NewGuid(), WeightMax = 57 };
        tournament.Groups.Add(group);
        var match = new WrestlingMatch
        {
            GroupID = group.ID, GroupName = "2012–2013 гг. 57 кг.", RoundName = "1/4 финала", Status = MatchStatusEnum.Completed, IsRedWon = true,
            WrestlerInRed = new Wrestler { ID = Guid.NewGuid(), LastName = "АБДУБЕРДЫМУХАМЕДОВ", FirstName = "Михаил", TeamID = team.ID },
            WrestlerInBlue = new Wrestler { ID = Guid.NewGuid(), LastName = "Соколов", FirstName = "Александр" }
        };
        var context = new DataContext { Tournament = tournament, Group = group, WrestlingMatch = match };
        di.Add<IDataContext>(context);
        var vm = new ScoreScreenViewModel(di);
        vm.InitData();
        return (vm, context, team);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public Task Correct_winner_side_and_exact_scoreboard_category_are_presented(bool red) => OnSta(() =>
    {
        var (vm, context, _) = Build();
        var match = context.WrestlingMatch;
        match.IsRedWon = red;
        try
        {
            vm.ShowWinner(match);

            vm.Winner.Should().BeSameAs(red ? match.WrestlerInRed : match.WrestlerInBlue);
            vm.WinnerGroupLabel.Should().Be(vm.GroupLabel).And.Be(match.GroupName);
            vm.WinnerGroupLabel.Should().NotContain("ДО");
            vm.WinnerRoundName.Should().Be(vm.RoundName).And.Be(match.RoundName);
            vm.WinnerColorBrush.Color.Should().Be(red ? Color.FromRgb(237, 67, 85) : Color.FromRgb(57, 121, 234));
            vm.IsWinnerDialogVisible.Should().BeTrue();
            vm.IsMainScreenVisible.Should().BeFalse();
        }
        finally { vm.EndWinnerAnnouncement(); }
    });

    [Theory]
    [InlineData(1920, 1080, "Соколов")]
    [InlineData(1920, 1080, "АБДУБЕРДЫМУХАМЕДОВ")]
    [InlineData(1280, 720, "АБДУБЕРДЫМУХАМЕДОВ")]
    public Task Real_view_fits_names_and_long_team_city_and_category_without_changing_name_font(int width, int height, string surname) => OnSta(() =>
    {
        var (vm, context, team) = Build();
        team.ShortName = "Спортивная школа олимпийского резерва имени Ивана Ярыгина";
        team.City = "Петропавловск-Камчатский";
        context.WrestlingMatch.WrestlerInRed.LastName = surname;
        context.WrestlingMatch.WrestlerInRed.FirstName = "Александр-Мухаммед";
        context.WrestlingMatch.RoundName = "УТЕШЕНИЕ КРУГ 1";
        try
        {
            vm.ShowWinner(context.WrestlingMatch);
            var view = new WinnerAnnouncementView { DataContext = vm };
            view.Measure(new Size(width, height));
            view.Arrange(new Rect(0, 0, width, height));
            view.UpdateLayout();
            var last = (TextBlock)view.FindName("WinnerLastName");
            var first = (TextBlock)view.FindName("WinnerFirstName");
            last.Text.Should().Be(surname.ToUpperInvariant());
            first.Text.Should().Be("Александр-Мухаммед");
            first.FontFamily.Should().Be(last.FontFamily);
            first.FontWeight.Should().Be(last.FontWeight).And.Be(FontWeights.Bold);
            first.FontSize.Should().Be(last.FontSize);
            first.Margin.Top.Should().BeNegative("the name lines are brought closer together");
            var lastScale = last.TransformToAncestor(view).Transform(new Point(1, 0))
                - last.TransformToAncestor(view).Transform(new Point(0, 0));
            var firstScale = first.TransformToAncestor(view).Transform(new Point(1, 0))
                - first.TransformToAncestor(view).Transform(new Point(0, 0));
            firstScale.X.Should().BeApproximately(lastScale.X, .001);

            foreach (var name in new[] { "WinnerLastName", "WinnerFirstName", "WinnerTeam", "WinnerCity", "WinnerCategory", "WinnerRound" })
            {
                var text = (TextBlock)view.FindName(name);
                var bounds = text.TransformToAncestor(view).TransformBounds(new Rect(text.RenderSize));
                bounds.Left.Should().BeGreaterThanOrEqualTo(0);
                bounds.Top.Should().BeGreaterThanOrEqualTo(0);
                bounds.Right.Should().BeLessThanOrEqualTo(width + .1);
                bounds.Bottom.Should().BeLessThanOrEqualTo(height + .1);
            }
            var round = (TextBlock)view.FindName("WinnerRound");
            var category = (TextBlock)view.FindName("WinnerCategory");
            round.Text.Should().Be("УТЕШЕНИЕ КРУГ 1");
            round.FontSize.Should().Be(category.FontSize);
            round.FontFamily.Should().Be(category.FontFamily);
            round.FontWeight.Should().Be(category.FontWeight);
            round.Foreground.Should().Be(category.Foreground);
            var roundBounds = round.TransformToAncestor(view).TransformBounds(new Rect(round.RenderSize));
            var categoryBounds = category.TransformToAncestor(view).TransformBounds(new Rect(category.RenderSize));
            roundBounds.Left.Should().BeGreaterThan(width * .35);
            roundBounds.Right.Should().BeLessThan(categoryBounds.Left);
            (roundBounds.Left + roundBounds.Width / 2).Should().BeApproximately(width / 2.0, .1);
            ((SolidColorBrush)view.Background).Color.Should().Be(Colors.Black);
            var city = (TextBlock)view.FindName("WinnerCity");
            var teamText = (TextBlock)view.FindName("WinnerTeam");
            city.TransformToAncestor(view).Transform(new Point()).Y.Should()
                .BeGreaterThan(teamText.TransformToAncestor(view).Transform(new Point()).Y);
            foreach (var name in new[] { "WinnerColorIndicator", "WinnerHeaderRule", "WinnerDetailsRule" })
                ((Border)view.FindName(name)).Background.Should().BeSameAs(vm.WinnerColorBrush);
        }
        finally { vm.EndWinnerAnnouncement(); }
    });

    [Fact]
    public Task Reused_view_refits_both_header_fields_for_changed_and_empty_text() => OnSta(() =>
    {
        var (vm, context, _) = Build();
        var view = new WinnerAnnouncementView { DataContext = vm };
        try
        {
            foreach (var roundName in new[] { "Финал", "УТЕШЕНИЕ КРУГ 1", "Предварительный круг", null, "1/2 финала" })
            {
                context.WrestlingMatch.RoundName = roundName;
                context.WrestlingMatch.GroupName = roundName == null ? null : "2012–2013 гг. 57 кг.";
                vm.ShowWinner(context.WrestlingMatch);
                view.Measure(new Size(1920, 1080));
                view.Arrange(new Rect(0, 0, 1920, 1080));
                view.UpdateLayout();
                var round = (TextBlock)view.FindName("WinnerRound");
                var category = (TextBlock)view.FindName("WinnerCategory");
                round.Text.Should().Be(roundName ?? string.Empty);
                round.FontSize.Should().Be(category.FontSize);
                foreach (var text in new[] { round, category })
                {
                    var parent = (Grid)text.Parent;
                    var bounds = text.TransformToAncestor(parent).TransformBounds(new Rect(text.RenderSize));
                    bounds.Left.Should().BeGreaterThanOrEqualTo(-.1);
                    bounds.Right.Should().BeLessThanOrEqualTo(parent.ActualWidth + .1);
                    bounds.Top.Should().BeGreaterThanOrEqualTo(-.1);
                    bounds.Bottom.Should().BeLessThanOrEqualTo(parent.ActualHeight + .1);
                }
            }
        }
        finally { vm.EndWinnerAnnouncement(); }
    });

    [Fact]
    public Task Missing_team_or_category_does_not_reuse_previous_announcement_details() => OnSta(() =>
    {
        var (vm, context, team) = Build();
        try
        {
            vm.ShowWinner(context.WrestlingMatch);
            vm.WinnerTeamEmblem.Should().Be(team.EmblemPath);
            vm.WinnerTeamName.Should().Be(team.ShortName);
            vm.WinnerTeamCity.Should().Be(team.City);
            var match = context.WrestlingMatch;
            match.IsRedWon = false;
            match.WrestlerInBlue.TeamID = Guid.NewGuid();
            match.WrestlerInBlue.TeamName = "Другая команда";
            match.WrestlerInBlue.TeamCity = "Другой город";
            match.GroupName = null;
            match.RoundName = null;

            vm.ShowWinner(match);

            vm.WinnerTeamEmblem.Should().BeEmpty();
            vm.WinnerGroupLabel.Should().BeEmpty();
            vm.WinnerRoundName.Should().BeEmpty();
            vm.WinnerTeamName.Should().Be("Другая команда");
            vm.WinnerTeamCity.Should().Be("Другой город");
        }
        finally { vm.EndWinnerAnnouncement(); }
        vm.Winner.Should().BeNull();
        vm.WinnerTeamCity.Should().BeEmpty();
        vm.WinnerTeamName.Should().BeEmpty();
        vm.WinnerRoundName.Should().BeEmpty();
        vm.IsMainScreenVisible.Should().BeTrue();
    });

    [Fact]
    public Task Winner_remains_after_five_seconds_and_repeated_announcement_until_next_match_loads() => OnSta(() =>
    {
        var (vm, context, _) = Build();
        try
        {
            vm.ShowWinner(context.WrestlingMatch);
            var frame = new DispatcherFrame();
            var observationTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
            observationTimer.Tick += (_, _) => { observationTimer.Stop(); frame.Continue = false; };
            observationTimer.Start();
            Dispatcher.PushFrame(frame);
            vm.IsWinnerDialogVisible.Should().BeTrue();
            vm.Winner.Should().BeSameAs(context.WrestlingMatch.WrestlerInRed);
            context.WrestlingMatch.IsRedWon = false;
            vm.ShowWinner(context.WrestlingMatch);
            vm.IsWinnerDialogVisible.Should().BeTrue();
            vm.Winner.Should().BeSameAs(context.WrestlingMatch.WrestlerInBlue);

            context.WrestlingMatch = new WrestlingMatch
            {
                GroupName = "Следующая категория", RoundName = "1/2 финала",
                WrestlerInRed = new Wrestler { LastName = "Иванов", FirstName = "Иван" },
                WrestlerInBlue = new Wrestler { LastName = "Петров", FirstName = "Пётр" }
            };
            vm.InitData();
            vm.IsWinnerDialogVisible.Should().BeFalse();
            vm.IsMainScreenVisible.Should().BeTrue();
            vm.Winner.Should().BeNull();
            vm.WinnerTeamName.Should().BeEmpty();
            vm.GroupLabel.Should().Be("Следующая категория");
            vm.RoundName.Should().Be("1/2 финала");
        }
        finally { vm.EndWinnerAnnouncement(); }
    });

    [Fact]
    public Task Pending_mutual_or_empty_results_do_not_announce_a_winner() => OnSta(() =>
    {
        var (vm, context, _) = Build();
        var match = context.WrestlingMatch;
        match.Status = MatchStatusEnum.Pending;
        vm.ShowWinner(match);
        match.Status = MatchStatusEnum.Completed;
        match.IsRedWon = null;
        vm.ShowWinner(match);
        match.IsRedWon = true;
        match.WrestlerInRed = null;
        vm.ShowWinner(match);
        vm.IsWinnerDialogVisible.Should().BeFalse();
        vm.IsMainScreenVisible.Should().BeTrue();
    });

    private static Task OnSta(Action action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { action(); completion.TrySetResult(); }
            catch (Exception ex) { completion.TrySetException(ex); }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task.WaitAsync(TimeSpan.FromSeconds(30));
    }
}
