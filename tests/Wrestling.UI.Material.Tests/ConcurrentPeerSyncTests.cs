using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using FluentAssertions;
using Wrestling.Data;
using Wrestling.DataAccess;
using Wrestling.Entities;
using Wrestling.Entities.Bracket;
using Wrestling.Providers;
using Wrestling.Providers.Network;
using Wrestling.UI.Material.Model;
using Xunit;
using WTournament = Wrestling.Entities.Tournament;

namespace Wrestling.UI.Material.Tests;

public class ConcurrentPeerSyncTests
{
    [Theory]
    [InlineData(0, 1, 2)]
    [InlineData(0, 2, 1)]
    [InlineData(1, 0, 2)]
    [InlineData(1, 2, 0)]
    [InlineData(2, 0, 1)]
    [InlineData(2, 1, 0)]
    public Task Four_mats_merge_during_admin_save_without_lost_results(int a, int b, int c) =>
        OnDispatcher(async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "wrestling-sync-race-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var servers = new List<TournamentHttpServer>();
            var storage = new PausedStorage();
            try
            {
                var adapter = new EntityToInfoAdapter();
                var manager = new TournamentsManager(new TournamentDataAccess(storage), adapter);
                var local = BuildTournament();
                var adminPath = Path.Combine(dir, "admin.wrt");
                manager.SaveToFile(local, adminPath).Should().BeTrue();
                var remotes = new List<WTournament>();
                var peers = new List<DiscoveredPeer>();
                for (var i = 0; i < 4; i++)
                {
                    var remote = adapter.GetEntityFromInfo(adapter.GetInfoFromEntity(local));
                    var processor = new RoundRobinGroupBracketProcessor();
                    processor.LoadTournamentGroup(remote, remote.Groups[i]);
                    var match = Match(remote, i);
                    match.PointsRed = i + 2;
                    processor.CompleteMatch(match, true, MatchWinTypeEnum.PointsWin);
                    match.Version = 1;
                    var path = Path.Combine(dir, "mat" + i + ".wrt");
                    manager.SaveToFile(remote, path).Should().BeTrue();
                    remotes.Add(remote);
                    var server = new TournamentHttpServer();
                    servers.Add(server);
                    server.SetServedTournament(local.ID.Value, path);
                    server.Start(0);
                    server.ActualPort.Should().NotBeNull();
                    peers.Add(Peer(local.ID.Value, PeerStateHasher.Compute(remote),
                        "http://127.0.0.1:" + server.ActualPort + "/tournament/" + local.ID + ".wrt"));
                }

                var importer = new ControlledImporter(new TournamentImporter(manager,
                    new GroupBracketProcessorFactory(() => new RoundRobinGroupBracketProcessor()), new MatMatchNumbersGenerator()),
                    peers.Select(p => p.HttpUrl), Dispatcher.CurrentDispatcher);
                using var service = new PeerSyncService(null, new DataContext { Tournament = local }, importer,
                    manager, null, Dispatcher.CurrentDispatcher);

                // A manual save is already writing when every mat advertises a result.
                var manual = manager.SaveToFileAsync(local, adminPath);
                await storage.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
                var pulls = peers.Select(service.HandlePeerAsync).ToArray();
                await Task.WhenAll(importer.Ready.Values.Select(t => t.Task)).WaitAsync(TimeSpan.FromSeconds(10));
                foreach (var index in new[] { a, b, c, 3 })
                {
                    importer.Release[peers[index].HttpUrl].TrySetResult();
                    await importer.Applied[index].Task.WaitAsync(TimeSpan.FromSeconds(10));
                }
                for (var i = 0; i < 4; i++) Match(local, i).Version.Should().Be(1);
                storage.AsyncWrites.Should().Be(1, "merged snapshots must queue behind the manual save");
                storage.Release.TrySetResult();
                (await manual).Should().BeTrue();
                await Task.WhenAll(pulls).WaitAsync(TimeSpan.FromSeconds(10));
                storage.AsyncWrites.Should().Be(5);
                var reopened = await manager.LoadFromFileAsync(adminPath);
                for (var i = 0; i < 4; i++)
                {
                    Match(reopened, i).Status.Should().Be(MatchStatusEnum.Completed);
                    Match(reopened, i).Version.Should().Be(1);
                    Match(reopened, i).PointsRed.Should().Be(i + 2);
                }
                PeerStateHasher.Compute(reopened).Should().Be(PeerStateHasher.Compute(local));

                // Old mat snapshots cannot roll back the union; an authoritative
                // revert from one mat must still preserve the other three results.
                var realImporter = new TournamentImporter(manager,
                    new GroupBracketProcessorFactory(() => new RoundRobinGroupBracketProcessor()), new MatMatchNumbersGenerator());
                foreach (var remote in remotes)
                    realImporter.Apply(local, ImportPlan.Proceed(remote)).Outcome.Should().Be(ImportOutcome.NoNewData);
                var revert = new RoundRobinGroupBracketProcessor();
                revert.LoadTournamentGroup(remotes[0], remotes[0].Groups[0]);
                revert.RevertMatch(Match(remotes[0], 0));
                Match(remotes[0], 0).Version = 2;
                realImporter.Apply(local, ImportPlan.Proceed(remotes[0])).Outcome.Should().Be(ImportOutcome.Imported);
                (await manager.SaveToFileAsync(local, adminPath)).Should().BeTrue();
                reopened = await manager.LoadFromFileAsync(adminPath);
                Match(reopened, 0).Status.Should().Be(MatchStatusEnum.Pending);
                Match(reopened, 0).Version.Should().Be(2);
                for (var i = 1; i < 4; i++) Match(reopened, i).Status.Should().Be(MatchStatusEnum.Completed);
            }
            finally
            {
                storage.Release.TrySetResult();
                foreach (var server in servers) server.Dispose();
                Directory.Delete(dir, true);
            }
        });

    private static WTournament BuildTournament()
    {
        var tournament = new WTournament(new GlobalSettings()) { ID = Guid.NewGuid(), Name = "Four mats" };
        for (var i = 0; i < 4; i++)
        {
            var mat = new Mat { ID = Guid.NewGuid(), Name = "Mat " + i };
            var group = new AgeWeightGroup
            {
                ID = Guid.NewGuid(), MatID = mat.ID, BirthYearMin = 2005, BirthYearMax = 2006,
                WeightMax = 50 + i * 5, MaxRoundSecond = 120
            };
            tournament.Groups.Add(group);
            tournament.Mats.Add(mat);
            mat.Groups.Add(group);
            for (var w = 0; w < 2; w++)
            {
                var wrestler = new Wrestler
                {
                    ID = Guid.NewGuid(), GroupID = group.ID, LastName = "W" + i + w,
                    IsEntryFeePaid = true, IsWeightApproved = true, SeedNumber = w + 1
                };
                group.Wrestlers.Add(wrestler);
                tournament.Wrestlers.Add(wrestler);
            }
            new RoundRobinGroupBracketProcessor().Generate(tournament, group);
        }
        return tournament;
    }

    private static WrestlingMatch Match(WTournament tournament, int group) =>
        tournament.Groups[group].Bracket.Rounds.SelectMany(r => r.RoundMatches).Single();

    private static DiscoveredPeer Peer(Guid tournamentId, string hash, string url)
    {
        var peer = (DiscoveredPeer)Activator.CreateInstance(typeof(DiscoveredPeer),
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            null, new object[] { Guid.NewGuid(), tournamentId }, null);
        typeof(DiscoveredPeer).GetProperty("StateHash").SetValue(peer, hash);
        typeof(DiscoveredPeer).GetProperty("HttpUrl").SetValue(peer, url);
        return peer;
    }

    private static Task OnDispatcher(Func<Task> body)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            dispatcher.BeginInvoke(new Action(async () =>
            {
                try { await body(); completion.TrySetResult(); }
                catch (Exception ex) { completion.TrySetException(ex); }
                finally { dispatcher.BeginInvokeShutdown(DispatcherPriority.Background); }
            }));
            Dispatcher.Run();
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task.WaitAsync(TimeSpan.FromSeconds(45));
    }

    private sealed class ControlledImporter : ITournamentImporter
    {
        private readonly TournamentImporter _inner;
        private readonly Dispatcher _dispatcher;
        private readonly string[] _urls;
        public Dictionary<string, TaskCompletionSource> Ready { get; } = new();
        public Dictionary<string, TaskCompletionSource> Release { get; } = new();
        public TaskCompletionSource[] Applied { get; } = Enumerable.Range(0, 4).Select(_ => Signal()).ToArray();
        public ControlledImporter(TournamentImporter inner, IEnumerable<string> urls, Dispatcher dispatcher)
        {
            _inner = inner;
            _dispatcher = dispatcher;
            _urls = urls.ToArray();
            foreach (var url in _urls) { Ready[url] = Signal(); Release[url] = Signal(); }
        }
        public async Task<ImportPlan> PrepareAsync(WTournament target, string fileName, CancellationToken cancellationToken = default)
        {
            _dispatcher.CheckAccess().Should().BeFalse();
            var plan = await _inner.PrepareAsync(target, fileName, cancellationToken);
            plan.NeedsApply.Should().BeTrue();
            Ready[fileName].TrySetResult();
            await Release[fileName].Task.WaitAsync(cancellationToken);
            return plan;
        }
        public ImportResult Apply(WTournament target, ImportPlan plan)
        {
            _dispatcher.CheckAccess().Should().BeTrue();
            var changedGroup = Enumerable.Range(0, 4).Single(i => Match(plan.Remote, i).Version == 1);
            var result = _inner.Apply(target, plan);
            result.Outcome.Should().Be(ImportOutcome.Imported);
            Applied[changedGroup].TrySetResult();
            return result;
        }
    }

    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private sealed class PausedStorage : IStorageDataAccess
    {
        private readonly JsonStorageDataAccess _inner = new();
        public TaskCompletionSource Entered { get; } = Signal();
        public TaskCompletionSource Release { get; } = Signal();
        public int AsyncWrites;
        public async Task<bool> SaveToFileAsync<T>(T item, string path, CancellationToken ct = default)
        {
            if (Interlocked.Increment(ref AsyncWrites) == 1)
            {
                Entered.TrySetResult();
                await Release.Task.WaitAsync(ct);
                // The first snapshot predates every merge, even when serialized later.
                ((TournamentInfo)(object)item).Groups.SelectMany(g => g.Bracket.Rounds)
                    .SelectMany(r => r.RoundMatches).Should().OnlyContain(m => m.Version == 0);
            }
            return await _inner.SaveToFileAsync(item, path, ct);
        }
        public bool SaveToFile<T>(T item, string path) => _inner.SaveToFile(item, path);
        public T ReadFromFile<T>(string path) => _inner.ReadFromFile<T>(path);
        public Task<T> ReadFromFileAsync<T>(string path, CancellationToken ct = default) => _inner.ReadFromFileAsync<T>(path, ct);
        public void ProcessDirectory<T>(string path, ref List<T> list, string mask) => _inner.ProcessDirectory(path, ref list, mask);
        public void SaveToStorage<T>(T item, string folder, string name) => _inner.SaveToStorage(item, folder, name);
        public IEnumerable<string> GetFileNamesInDirectory(string path, string mask) => _inner.GetFileNamesInDirectory(path, mask);
    }
}
