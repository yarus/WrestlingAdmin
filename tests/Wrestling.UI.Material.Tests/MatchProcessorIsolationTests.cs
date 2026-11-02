using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Windows;
using MvvmDialogs;
using FluentAssertions;
using Wrestling.Entities;
using Wrestling.Entities.Bracket;
using Wrestling.Providers;
using Wrestling.DataAccess;
using Wrestling.Providers.Network;
using Wrestling.UI.Material.Match;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Material.ScoreScreen;
using Wrestling.UI.Material.Tests.Fakes;
using Xunit;

namespace Wrestling.UI.Material.Tests;

public class MatchProcessorIsolationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task Confirming_an_open_result_after_another_category_changes_keeps_its_own_bracket(bool recalculate) => Verify(recalculate, false);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task Reverting_an_open_result_after_another_category_changes_keeps_its_own_bracket(bool recalculate) => Verify(recalculate, true);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task Http_sync_of_a_result_or_registration_cannot_redirect_the_open_match(bool registration) => Verify(true, false, registration ? "registration" : "result");

    [Fact]
    public Task A_match_replaced_while_its_result_is_open_cannot_be_approved() => Verify(false, false, "replace");

    private static Task Verify(bool recalculate, bool revert, string pullMode = null) => OnDispatcher(async () =>
    {
        var tournament = new Wrestling.Entities.Tournament(new GlobalSettings()) { ID = Guid.NewGuid() };
        var processor = new OlympicWithConsolationFromFinalistsGroupBracketProcessor();
        var processors = new GroupBracketProcessorFactory(() => new OlympicWithConsolationFromFinalistsGroupBracketProcessor());
        for (var i = 0; i < 3; i++)
        {
            var group = new AgeWeightGroup { ID = Guid.NewGuid(), WeightMax = 30 + i };
            tournament.Groups.Add(group);
            for (var j = 0; j < 8; j++)
            {
                var wrestler = new Wrestler { ID = Guid.NewGuid(), GroupID = group.ID, SeedNumber = j + 1, LastName = $"W{i}-{j}" };
                group.Wrestlers.Add(wrestler); tournament.Wrestlers.Add(wrestler);
            }
            processor.Generate(tournament, group);
        }
        var localGroup = tournament.Groups[0];
        processor.Load(tournament, localGroup);
        foreach (var qf in localGroup.Bracket.MainRounds().First().RoundMatches)
            processor.CompleteMatch(qf, true, MatchWinTypeEnum.PointsWin);
        var match = localGroup.Bracket.MainRounds().ToList()[1].RoundMatches[0];
        var winner = match.WrestlerInBlue;
        if (revert) processor.CompleteMatch(match, false, MatchWinTypeEnum.Tushe);
        var adapter = new EntityToInfoAdapter();
        var remote = adapter.GetEntityFromInfo(adapter.GetInfoFromEntity(tournament));
        var remoteProcessor = new OlympicWithConsolationFromFinalistsGroupBracketProcessor();
        remoteProcessor.Load(remote, remote.Groups[1]);
        var changed = remote.Groups[1].Bracket.MainRounds().First().RoundMatches[0];
        if (pullMode == "registration")
        {
            remote.Wrestlers[8].Level = WrestlerLevelEnum.KMS;
            remote.Wrestlers[8].Timestamp = DateTime.UtcNow;
        }
        else
        {
            remoteProcessor.CompleteMatch(changed, true, MatchWinTypeEnum.PointsWin);
            changed.Version = 1;
        }

        var di = TestContainerBuilder.MakeDefault();
        di.Add<IDialogService>(DispatchProxy.Create<IDialogService, ConfirmDialog>());
        di.Add<IGroupBracketProcessorFactory>(processors);
        var context = new DataContext { Tournament = tournament, Group = localGroup, WrestlingMatch = match };
        di.Add<IDataContext>(context);
        var results = new ResultsService(processors, null, null);
        di.Add<IResultsService>(results);
        var score = new ScoreScreenViewModel(di); di.Add<ScoreScreenViewModel>(score); score.InitData();
        var vm = new MatchResultsViewModel(di); vm.InitData(); vm.WinType = MatchWinTypeEnum.Tushe; vm.Winner = winner.ID;

        if (pullMode == "replace")
        {
            processor.Generate(tournament, localGroup);
            var oldVersion = match.Version;
            Func<Task> approve = () => vm.ApproveAsync();
            await approve.Should().ThrowAsync<BracketStateException>();
            match.Version.Should().Be(oldVersion);
            match.Status.Should().Be(MatchStatusEnum.Pending);
            match.WinType.Should().BeNull();
            return;
        }
        if (pullMode != null) await PullOverHttp(context, remote, processors, results);
        else
        {
            new TournamentImporter(null, processors, null).Apply(tournament, ImportPlan.Proceed(remote)).Outcome.Should().Be(ImportOutcome.Imported);
            if (recalculate) results.Recalculate(tournament);
        }
        if (revert) await vm.RejectAsync();
        else await vm.ApproveAsync();

        var final = localGroup.Bracket.MainRounds().Last().RoundMatches.Single();
        if (revert)
        {
            match.Status.Should().Be(MatchStatusEnum.Pending);
            new[] { final.WrestlerInRed, final.WrestlerInBlue }.Should().NotContain(winner);
        }
        else new[] { final.WrestlerInRed, final.WrestlerInBlue }.Should().Contain(winner);
        if (pullMode == "registration") tournament.Wrestlers[8].Level.Should().Be(WrestlerLevelEnum.KMS);
        else tournament.Groups[1].Bracket.MainRounds().First().RoundMatches[0].Status.Should().Be(MatchStatusEnum.Completed);
        foreach (var group in tournament.Groups)
            group.Bracket.Rounds.SelectMany(r => r.RoundMatches).SelectMany(m => new[] { m.WrestlerInRed, m.WrestlerInBlue })
                .Where(w => w != null).Should().OnlyContain(w => group.Wrestlers.Contains(w));
    });

    private static async Task PullOverHttp(DataContext context, Wrestling.Entities.Tournament remote,
        IGroupBracketProcessorFactory factory, IResultsService results)
    {
        var file = Path.Combine(Path.GetTempPath(), "wrestling-isolation-" + Guid.NewGuid().ToString("N") + ".wrt");
        try
        {
            remote.Settings.IsBackupEnabled = false;
            var manager = new TournamentsManager(new TournamentDataAccess(new JsonStorageDataAccess()), new EntityToInfoAdapter());
            manager.SaveToFile(remote, file).Should().BeTrue();
            using var server = new TournamentHttpServer();
            server.SetServedTournament(remote.ID.Value, file); server.Start(0);
            var peer = (DiscoveredPeer)Activator.CreateInstance(typeof(DiscoveredPeer), BindingFlags.NonPublic | BindingFlags.Instance,
                null, new object[] { Guid.NewGuid(), remote.ID.Value }, null);
            typeof(DiscoveredPeer).GetProperty("StateHash").SetValue(peer, PeerStateHasher.Compute(remote));
            typeof(DiscoveredPeer).GetProperty("HttpUrl").SetValue(peer, $"http://127.0.0.1:{server.ActualPort}/tournament/{remote.ID}.wrt");
            using var service = new PeerSyncService(null, context, new TournamentImporter(manager, factory, null), manager, results, Dispatcher.CurrentDispatcher);
            await service.HandlePeerAsync(peer);
        }
        finally { File.Delete(file); }
    }

    public class ConfirmDialog : DispatchProxy
    {
        protected override object Invoke(MethodInfo method, object[] args) => method.ReturnType == typeof(MessageBoxResult) ? MessageBoxResult.OK : null;
    }

    private static Task OnDispatcher(Func<Task> body)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            dispatcher.BeginInvoke(new Action(async () =>
            {
                try { await body(); done.TrySetResult(); }
                catch (Exception ex) { done.TrySetException(ex); }
                finally { dispatcher.BeginInvokeShutdown(DispatcherPriority.Background); }
            }));
            Dispatcher.Run();
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        return done.Task.WaitAsync(TimeSpan.FromSeconds(30));
    }
}
