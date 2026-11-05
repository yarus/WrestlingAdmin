using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Threading;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using MvvmDialogs;
using FluentAssertions;
using Wrestling.Entities;
using Wrestling.Entities.Bracket;
using Wrestling.Providers.Network;
using Wrestling.Providers;
using Wrestling.UI.Material.Match;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Material.Tests.Fakes;
using Wrestling.UI.Material.ScoreScreen;
using Wrestling.UI.Material.Tournament.Progress.Brackets;
using Wrestling.UI.Material.Tournament.Progress.Schedule;
using Wrestling.UI.Utils;
using Xunit;

namespace Wrestling.UI.Material.Tests;

public sealed class MatchActivityTests
{
    private sealed class Discovery : IPeerDiscoveryService
    {
        public PeerRegistry Registry { get; } = new PeerRegistry();
        public ActiveMatchIdentity LocalMatch { get; private set; }
        public List<ActiveMatchIdentity> Published { get; } = new();
        public int? ActualPort => null;
        public event EventHandler<DiscoveredPeer> PeerUpserted;
        public event EventHandler<DiscoveredPeer> PeerExpired;
        public event EventHandler<string> DiagnosticMessage { add { } remove { } }
        public Discovery(Guid tournament)
        {
            Registry.SetContext(Guid.NewGuid(), tournament);
            Registry.PeerUpserted += (_, peer) => PeerUpserted?.Invoke(this, peer);
            Registry.PeerExpired += (_, peer) => PeerExpired?.Invoke(this, peer);
        }
        public IReadOnlyCollection<DiscoveredPeer> SnapshotPeers() => Registry.Snapshot();
        public void SetActiveMatch(ActiveMatchIdentity match) { LocalMatch = match; Published.Add(match); }
        public void StartForTournament(int port, Guid tournamentId, string title, string name, string url, Func<string> hash = null) { }
        public void Stop() { }
        public void Dispose() { }
    }

    private sealed class Setup : IDisposable
    {
        public TestDiContainer Di { get; } = TestContainerBuilder.MakeDefault();
        public IDataContext Context => Di.Resolve<IDataContext>();
        public AgeWeightGroup Group { get; } = new AgeWeightGroup { ID = Guid.NewGuid(), BracketVersion = 7,
            Bracket = new GroupBracket { BracketTypeCode = BracketTypeEnum.RoundRobin.ToString() } };
        public WrestlingMatch[] Matches { get; }
        public Discovery Discovery { get; }
        public MatchActivityService Activity { get; }
        public List<string> Notifications { get; } = new();
        public Setup()
        {
            Context.Tournament = new Wrestling.Entities.Tournament(new GlobalSettings()) { ID = Guid.NewGuid() };
            Context.Tournament.Groups.Add(Group);
            var mat = new Mat { ID = Guid.NewGuid(), Name = "Ковер A" };
            mat.Groups.Add(Group); Context.Tournament.Mats.Add(mat);
            var round = new GroupRound(); Group.Bracket.Rounds.Add(round);
            Matches = Enumerable.Range(1, 3).Select(n => new WrestlingMatch { GroupID = Group.ID, MatchNumber = n,
                RoundNumber = 1, BracketNumber = n, WrestlerInRed = new Wrestler { ID = Guid.NewGuid(), LastName = "Red" },
                WrestlerInBlue = new Wrestler { ID = Guid.NewGuid(), LastName = "Blue" } }).ToArray();
            foreach (var match in Matches) round.RoundMatches.Add(match);
            Discovery = new Discovery(Context.Tournament.ID.Value);
            Activity = new MatchActivityService(Discovery, Context, notify: Notifications.Add);
            Di.Add<MatchActivityService>(Activity);
        }
        public ActiveMatchIdentity Key(int index) => new(Group.ID, Group.BracketVersion, Matches[index].BracketFullNumber);
        public PeerAdvertisement Announce(int index, string name = "Ковер B", Guid? instance = null, long revision = 1)
        {
            var ad = new PeerAdvertisement { InstanceId = instance ?? Guid.NewGuid(), TournamentId = Context.Tournament.ID.Value,
                NodeName = name, ComputerName = name + " PC", ActivityRevision = revision, ActiveMatch = Key(index), StateHash = "unchanged" };
            Ingest(ad); return ad;
        }
        public void Ingest(PeerAdvertisement ad) => Discovery.Registry.Ingest(ad, IPAddress.Loopback, DateTime.UtcNow);
        public void Dispose() => Activity.Dispose();
    }

    [Fact]
    public void Three_other_mats_open_different_bouts_from_the_first_mats_category()
    {
        using var s = new Setup();
        for (int i = 0; i < 3; i++) s.Announce(i, "Ковер " + (i + 2));
        for (int i = 0; i < 3; i++)
        {
            var status = s.Activity.GetStatus(s.Matches[i]);
            status.IsOccupied.Should().BeTrue();
            status.Label.Should().Be("Ковер " + (i + 2));
        }
        var ad = s.Discovery.SnapshotPeers().First();
        s.Ingest(new PeerAdvertisement { InstanceId = ad.InstanceId, TournamentId = ad.TournamentId, ActivityRevision = 2 });
        s.Activity.GetStatus(s.Matches[0]).IsOccupied.Should().BeFalse();
        s.Activity.GetStatus(s.Matches[1]).IsOccupied.Should().BeTrue();
        s.Activity.GetStatus(s.Matches[2]).IsOccupied.Should().BeTrue();
    }

    [Fact]
    public void Releasing_one_of_two_operators_preserves_the_others_presence()
    {
        using var s = new Setup();
        var first = s.Announce(0);
        s.Announce(0, "Ковер C");
        s.Activity.GetStatus(s.Matches[0]).Label.Should().Contain("Ковер B").And.Contain("Ковер C");
        first.ActiveMatch = null; first.ActivityRevision++; s.Ingest(first);
        s.Activity.GetStatus(s.Matches[0]).Label.Should().Be("Ковер C");
    }

    [Fact]
    public void Expired_presence_becomes_uncertain_then_explicit_release_clears_it_even_after_late_datagram()
    {
        using var s = new Setup();
        var ad = s.Announce(0);
        s.Discovery.Registry.Tick(DateTime.UtcNow.AddSeconds(20));
        s.Activity.GetStatus(s.Matches[0]).ConnectionLost.Should().BeTrue();
        s.Activity.GetStatus(s.Matches[0]).ToolTip.Should().NotBeNullOrEmpty();
        ad.ActiveMatch = null; ad.ActivityRevision = 2; s.Ingest(ad);
        s.Activity.GetStatus(s.Matches[0]).IsOccupied.Should().BeFalse();
        s.Discovery.Registry.Tick(DateTime.UtcNow.AddSeconds(20));
        ad.ActiveMatch = s.Key(0); ad.ActivityRevision = 1; s.Ingest(ad);
        s.Activity.GetStatus(s.Matches[0]).IsOccupied.Should().BeFalse();
    }

    [Fact]
    public void Reconnection_restores_live_presence_and_renaming_updates_its_label()
    {
        using var s = new Setup();
        var ad = s.Announce(0);
        s.Discovery.Registry.Tick(DateTime.UtcNow.AddSeconds(20));
        ad.NodeName = "Новое имя"; s.Ingest(ad);
        var status = s.Activity.GetStatus(s.Matches[0]);
        status.ConnectionLost.Should().BeFalse();
        status.Label.Should().Be("Новое имя");
    }

    [Fact]
    public void Completed_bouts_and_regenerated_brackets_do_not_display_old_presence()
    {
        using var s = new Setup();
        s.Announce(0);
        s.Matches[0].Status = MatchStatusEnum.Completed;
        s.Activity.GetStatus(s.Matches[0]).IsOccupied.Should().BeFalse();
        s.Matches[0].Status = MatchStatusEnum.Pending;
        s.Group.BracketVersion++;
        s.Activity.GetStatus(s.Matches[0]).IsOccupied.Should().BeFalse();
    }

    [Fact]
    public void Tournament_replacement_clears_local_and_remote_presence()
    {
        using var s = new Setup();
        s.Announce(0); s.Activity.Open(s.Matches[1]);
        s.Context.Tournament = null;
        s.Discovery.LocalMatch.Should().BeNull();
        s.Activity.GetStatus(s.Matches[0]).IsOccupied.Should().BeFalse();
    }

    [Fact]
    public void Local_presence_does_not_change_match_versions_or_state_hash_and_release_needs_no_result()
    {
        using var s = new Setup();
        var hash = PeerStateHasher.Compute(s.Context.Tournament);
        var version = s.Matches[0].Version;
        s.Activity.Open(s.Matches[0]);
        s.Discovery.LocalMatch.Should().Be(s.Key(0));
        PeerStateHasher.Compute(s.Context.Tournament).Should().Be(hash);
        s.Matches[0].Version.Should().Be(version);
        s.Activity.Release();
        s.Discovery.LocalMatch.Should().BeNull();
        s.Matches[0].Status.Should().Be(MatchStatusEnum.Pending);
        ((FakeTournamentsManager)s.Di.Resolve<Wrestling.Providers.ITournamentsManager>()).SaveAsyncCount.Should().Be(0);
    }

    [Fact]
    public void Simultaneous_open_is_reported_once_even_with_periodic_announcements()
    {
        using var s = new Setup();
        s.Activity.Open(s.Matches[0]);
        var ad = s.Announce(0); s.Ingest(ad); s.Ingest(ad);
        s.Notifications.Should().ContainSingle().Which.Should().Contain("Ковер B");
    }

    [Fact]
    public void Operators_with_duplicate_names_remain_distinguishable()
    {
        using var s = new Setup();
        s.Announce(0); s.Announce(0);
        var status = s.Activity.GetStatus(s.Matches[0]);
        status.Label.Split("Ковер B PC").Should().HaveCount(3);
        status.IsOccupied.Should().BeTrue();
    }

    [Fact]
    public Task Approving_result_releases_local_presence() => OnDispatcher(async () =>
    {
        using var s = new Setup();
        foreach (var wrestler in s.Matches.Take(2).SelectMany(m => new[] { m.WrestlerInRed, m.WrestlerInBlue }))
            s.Group.Wrestlers.Add(wrestler);
        var factory = s.Di.Resolve<IGroupBracketProcessorFactory>();
        factory.Create(BracketTypeEnum.RoundRobin.ToString()).Generate(s.Context.Tournament, s.Group);
        var match = s.Group.Bracket.Rounds.SelectMany(r => r.RoundMatches).First(m => m.IsMatchCanStart);
        s.Context.Group = s.Group; s.Context.WrestlingMatch = match;
        s.Di.Add<IResultsService>(new ResultsService(factory, null, null));
        var score = new ScoreScreenViewModel(s.Di); s.Di.Add<ScoreScreenViewModel>(score); score.InitData();
        s.Activity.Open(match);
        s.Discovery.LocalMatch.Should().NotBeNull();
        var results = new MatchResultsViewModel(s.Di); results.InitData();
        results.WinType = MatchWinTypeEnum.Tushe; results.Winner = match.WrestlerInRed.ID;
        await results.ApproveAsync();
        s.Discovery.LocalMatch.Should().BeNull();
        match.Status.Should().Be(MatchStatusEnum.Completed);
        score.EndWinnerAnnouncement();
    });

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Schedule_and_bracket_open_only_after_confirmation(bool brackets, bool confirmed)
    {
        using var s = new Setup(); s.Announce(0);
        ViewModelBase vm = brackets ? new BracketsViewModel(s.Di) : new ScheduleViewModel(s.Di);
        vm.InitData();
        int prompts = 0;
        vm.ConfirmMatchActivity = status => { prompts++; status.Label.Should().Be("Ковер B"); return Task.FromResult(confirmed); };
        if (brackets) await ((BracketsViewModel)vm).OpenMatchAsync(s.Matches[0]);
        else await ((ScheduleViewModel)vm).OpenMatchAsync(s.Matches[0]);
        prompts.Should().Be(1);
        var nav = (FakeNavigationService)s.Di.Resolve<INavigationService>();
        if (confirmed)
        {
            nav.NavigatedTo.Should().ContainSingle().Which.Should().Be(typeof(MatchControlViewModel));
            s.Context.WrestlingMatch.Should().BeSameAs(s.Matches[0]);
        }
        else
        {
            nav.NavigatedTo.Should().BeEmpty();
            s.Context.WrestlingMatch.Should().BeNull();
        }
        vm.NotifyNavigatingOut();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Replacing_bracket_while_confirming_prevents_opening_detached_match(bool brackets)
    {
        using var s = new Setup(); s.Announce(0);
        ViewModelBase vm = brackets ? new BracketsViewModel(s.Di) : new ScheduleViewModel(s.Di);
        vm.InitData();
        vm.ConfirmMatchActivity = _ => { s.Group.Bracket = new GroupBracket(); return Task.FromResult(true); };
        if (brackets) await ((BracketsViewModel)vm).OpenMatchAsync(s.Matches[0]);
        else await ((ScheduleViewModel)vm).OpenMatchAsync(s.Matches[0]);
        ((FakeNavigationService)s.Di.Resolve<INavigationService>()).NavigatedTo.Should().BeEmpty();
        vm.NotifyNavigatingOut();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task Match_control_keeps_presence_through_pause_and_result_selection_and_releases_only_confirmed_exit(bool exitConfirmed)
        => OnDispatcher(async () =>
    {
        using var s = new Setup();
        s.Di.Add<ScoreScreenViewModel>(new ScoreScreenViewModel(s.Di));
        var dialog = DispatchProxy.Create<IDialogService, ExitDialog>();
        ((ExitDialog)(object)dialog).Result = exitConfirmed ? MessageBoxResult.OK : MessageBoxResult.Cancel;
        s.Di.Add<IDialogService>(dialog);
        var nav = new NavigationService(s.Di) { ShellVm = new FakeShellViewModel() };
        s.Di.Add<INavigationService>(nav); nav.LoadNavigation();
        s.Context.Group = s.Group; s.Context.WrestlingMatch = s.Matches[0];
        nav.NavigateToView<MatchControlViewModel>();
        var control = nav.GetViewModel<MatchControlViewModel>();
        s.Discovery.LocalMatch.Should().Be(s.Key(0));
        control.StartCommand.Execute(null); control.StopCommand.Execute(null);
        s.Discovery.LocalMatch.Should().Be(s.Key(0));
        control.CompleteMatchCommand.Execute(null);
        nav.ShellVm.CurrentViewModel.Should().BeOfType<MatchResultsViewModel>();
        s.Discovery.LocalMatch.Should().Be(s.Key(0));
        nav.GetViewModel<MatchResultsViewModel>().BackCommand.Execute(null);
        s.Discovery.LocalMatch.Should().Be(s.Key(0));
        control.BackCommand.Execute(null);
        if (exitConfirmed)
        {
            s.Discovery.LocalMatch.Should().BeNull();
            s.Context.WrestlingMatch.Should().BeNull();
            nav.ShellVm.CurrentViewModel.Should().NotBeOfType<MatchControlViewModel>();
        }
        else
        {
            s.Discovery.LocalMatch.Should().Be(s.Key(0));
            nav.ShellVm.CurrentViewModel.Should().BeOfType<MatchControlViewModel>();
        }
        await Task.CompletedTask;
    });

    public class ExitDialog : DispatchProxy
    {
        public MessageBoxResult Result { get; set; }
        protected override object Invoke(MethodInfo targetMethod, object[] args)
            => targetMethod.ReturnType == typeof(MessageBoxResult) ? Result : null;
    }

    private static Task OnDispatcher(Func<Task> action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            dispatcher.BeginInvoke(new Action(async () =>
            {
                try { await action(); completion.TrySetResult(); }
                catch (Exception ex) { completion.TrySetException(ex); }
                finally { dispatcher.BeginInvokeShutdown(DispatcherPriority.Background); }
            }));
            Dispatcher.Run();
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        return completion.Task.WaitAsync(TimeSpan.FromSeconds(30));
    }
}
