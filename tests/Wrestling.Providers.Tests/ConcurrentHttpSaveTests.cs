using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Newtonsoft.Json;
using Wrestling.Data;
using Wrestling.DataAccess;
using Wrestling.Providers.Network;
using Xunit;

namespace Wrestling.Providers.Tests;

public class ConcurrentHttpSaveTests
{
    [Fact]
    public async Task Three_http_clients_read_complete_snapshots_during_repeated_saves()
    {
        var dir = Path.Combine(Path.GetTempPath(), "wrestling-http-race-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "admin.wrt");
        var id = Guid.NewGuid();
        var data = new TournamentDataAccess(new JsonStorageDataAccess());
        TournamentInfo Snapshot(int version) => new()
        {
            ID = id, MetaVersion = version, Name = version + new string('x', 256 * 1024),
            Settings = new GlobalSettingsInfo { IsBackupEnabled = false }
        };
        using var server = new TournamentHttpServer();
        var diagnostics = new ConcurrentQueue<string>();
        server.DiagnosticMessage += (_, message) => diagnostics.Enqueue(message);
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        try
        {
            data.SaveToFile(Snapshot(0), path).Should().BeTrue();
            server.SetServedTournament(id, path);
            server.Start(0);
            server.ActualPort.Should().NotBeNull();
            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var reads = Enumerable.Range(0, 3).Select(async _ =>
            {
                await start.Task;
                for (var i = 0; i < 40; i++)
                {
                    using var response = await http.GetAsync("http://127.0.0.1:" + server.ActualPort + "/tournament/" + id + ".wrt");
                    response.IsSuccessStatusCode.Should().BeTrue("status=" + response.StatusCode + "; " + string.Join("; ", diagnostics));
                    var json = await response.Content.ReadAsStringAsync();
                    var snapshot = JsonConvert.DeserializeObject<TournamentInfo>(json);
                    snapshot.ID.Should().Be(id);
                    snapshot.Name.Should().Be(snapshot.MetaVersion + new string('x', 256 * 1024));
                }
            }).ToArray();
            var writes = Task.Run(async () =>
            {
                await start.Task;
                for (var i = 1; i <= 80; i++)
                    (await data.SaveToFileAsync(Snapshot(i), path)).Should().BeTrue();
            });
            start.TrySetResult();
            await Task.WhenAll(reads.Append(writes)).WaitAsync(TimeSpan.FromSeconds(30));
            data.LoadFromFile(path).MetaVersion.Should().Be(80);
        }
        finally { server.Stop(); Directory.Delete(dir, true); }
    }
}
