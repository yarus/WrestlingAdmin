using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Wrestling.Entities;
using Wrestling.Entities.Bracket;
using Wrestling.Providers;
using Wrestling.Providers.Network;
using Wrestling.UI.Material.Model;
using Xunit;

namespace Wrestling.UI.Material.Tests;

// Verifies the HTTP-URI extension of TournamentImporter.PrepareAsync: sources
// prefixed with http/https are streamed into a temp file before being handed
// to the existing loader; failures (bad host, 404) are surfaced as
// FileUnavailable the same way missing SMB shares are today.
public sealed class TournamentImporterHttpTests
{
    private sealed class CapturingTournamentsManager : ITournamentsManager
    {
        public string LastLoadedPath { get; private set; }
        public byte[] LastLoadedBytes { get; private set; }
        public Entities.Tournament Response { get; set; }

        public Entities.Tournament LoadFromFile(string fileName) => Capture(fileName);
        public Task<Entities.Tournament> LoadFromFileAsync(string fileName) => Task.FromResult(Capture(fileName));

        private Entities.Tournament Capture(string path)
        {
            LastLoadedPath = path;
            try { if (File.Exists(path)) LastLoadedBytes = File.ReadAllBytes(path); }
            catch { LastLoadedBytes = null; }
            return Response;
        }

        public bool SaveToFile(Entities.Tournament item, string fileName) => throw new NotSupportedException();
        public Task<bool> SaveToFileAsync(Entities.Tournament item, string fileName) => throw new NotSupportedException();
    }

    private static int FindFreePort()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var port = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }

    private static Entities.Tournament MakeTarget(string name = "T", int groups = 0, DateTime? startDate = null)
    {
        var t = new Entities.Tournament(new GlobalSettings()) { Name = name, StartDate = startDate };
        for (int i = 0; i < groups; i++) t.Groups.Add(new AgeWeightGroup { ID = Guid.NewGuid() });
        return t;
    }

    [Fact]
    public async Task Http_source_streams_into_temp_file_and_proceeds_when_shape_matches()
    {
        var id = Guid.NewGuid();
        var servedContent = "{\"Name\":\"Ярыгин 2025\"}";
        var servedPath = Path.Combine(Path.GetTempPath(), "importer-http-" + Guid.NewGuid().ToString("N") + ".wrt");
        File.WriteAllBytes(servedPath, Encoding.UTF8.GetBytes(servedContent));

        var port = FindFreePort();
        using var server = new TournamentHttpServer();
        try
        {
            server.SetServedTournament(id, servedPath);
            server.Start(port);

            var target = MakeTarget(name: "Ярыгин");
            var mgr = new CapturingTournamentsManager
            {
                Response = MakeTarget(name: "Ярыгин")
            };
            var importer = new TournamentImporter(mgr, new List<IGroupBracketProcessor>());

            var plan = await importer.PrepareAsync(target, "http://127.0.0.1:" + port + "/tournament/" + id + ".wrt");

            plan.Remote.Should().NotBeNull();
            plan.ShortCircuit.Should().BeNull();
            mgr.LastLoadedPath.Should().NotBeNull();
            mgr.LastLoadedPath.Should().EndWith(".wrt");
            mgr.LastLoadedPath.Should().NotContain("http").And.NotContain("//");
            mgr.LastLoadedBytes.Should().NotBeNull();
            Encoding.UTF8.GetString(mgr.LastLoadedBytes).Should().Be(servedContent);
        }
        finally
        {
            server.Stop();
            File.Delete(servedPath);
        }
    }

    [Fact]
    public async Task Http_404_response_yields_FileUnavailable()
    {
        var port = FindFreePort();
        using var server = new TournamentHttpServer();
        try
        {
            // Server is started but no tournament is served → any GET returns 404.
            server.SetServedTournament(Guid.Empty, null);
            server.Start(port);

            var target = MakeTarget();
            var mgr = new CapturingTournamentsManager();
            var importer = new TournamentImporter(mgr, new List<IGroupBracketProcessor>());

            var plan = await importer.PrepareAsync(
                target,
                "http://127.0.0.1:" + port + "/tournament/" + Guid.NewGuid() + ".wrt");

            plan.ShortCircuit.Should().Be(ImportOutcome.FileUnavailable);
            mgr.LastLoadedPath.Should().BeNull("nothing was downloaded, so the loader is never called");
        }
        finally
        {
            server.Stop();
        }
    }

    [Fact]
    public async Task Unreachable_host_yields_FileUnavailable()
    {
        // Port that nobody is listening on. The HttpClient will fail fast with
        // a connection-refused HttpRequestException.
        var unusedPort = FindFreePort();
        var target = MakeTarget();
        var mgr = new CapturingTournamentsManager();
        var importer = new TournamentImporter(mgr, new List<IGroupBracketProcessor>());

        var plan = await importer.PrepareAsync(
            target,
            "http://127.0.0.1:" + unusedPort + "/tournament/" + Guid.NewGuid() + ".wrt");

        plan.ShortCircuit.Should().Be(ImportOutcome.FileUnavailable);
    }

    [Fact]
    public async Task Non_http_source_is_passed_through_unchanged()
    {
        // Ensures the existing UNC/local-path flow is not regressed — the
        // importer should pass the raw path to LoadFromFileAsync without
        // downloading anything.
        var target = MakeTarget();
        var mgr = new CapturingTournamentsManager
        {
            Response = MakeTarget()
        };
        var importer = new TournamentImporter(mgr, new List<IGroupBracketProcessor>());

        var plan = await importer.PrepareAsync(target, @"\\host\share\tournament.wrt");

        plan.Remote.Should().NotBeNull();
        mgr.LastLoadedPath.Should().Be(@"\\host\share\tournament.wrt");
    }

    [Fact]
    public async Task Compound_source_falls_back_to_second_candidate_when_first_is_unreachable()
    {
        // Simulates a peer entry like "http://dead:0/...|\\\\real\\share\\file" —
        // HTTP fetch fails (connection refused), importer falls back to the
        // second candidate (a valid local path backed by CapturingTournamentsManager).
        var unreachableHttp = "http://127.0.0.1:" + FindFreePort() + "/tournament/" + Guid.NewGuid() + ".wrt";
        var localPath = Path.Combine(Path.GetTempPath(), "compound-" + Guid.NewGuid().ToString("N") + ".wrt");
        File.WriteAllBytes(localPath, Encoding.UTF8.GetBytes("{}"));

        try
        {
            var target = MakeTarget();
            var mgr = new CapturingTournamentsManager
            {
                Response = MakeTarget()
            };
            var importer = new TournamentImporter(mgr, new List<IGroupBracketProcessor>());

            var plan = await importer.PrepareAsync(target, unreachableHttp + "|" + localPath);

            plan.Remote.Should().NotBeNull("HTTP was unreachable but the UNC/local candidate succeeded");
            mgr.LastLoadedPath.Should().Be(localPath);
        }
        finally
        {
            File.Delete(localPath);
        }
    }

    [Fact]
    public async Task Compound_source_where_all_candidates_fail_yields_FileUnavailable()
    {
        var port1 = FindFreePort();
        var port2 = FindFreePort();
        var unreachable1 = "http://127.0.0.1:" + port1 + "/tournament/" + Guid.NewGuid() + ".wrt";
        var unreachable2 = "http://127.0.0.1:" + port2 + "/tournament/" + Guid.NewGuid() + ".wrt";

        var target = MakeTarget();
        var mgr = new CapturingTournamentsManager();
        var importer = new TournamentImporter(mgr, new List<IGroupBracketProcessor>());

        var plan = await importer.PrepareAsync(target, unreachable1 + "|" + unreachable2);

        plan.ShortCircuit.Should().Be(ImportOutcome.FileUnavailable);
    }

    [Fact]
    public async Task Temp_file_is_deleted_after_http_fetch_completes()
    {
        var id = Guid.NewGuid();
        var servedPath = Path.Combine(Path.GetTempPath(), "importer-http-cleanup-" + Guid.NewGuid().ToString("N") + ".wrt");
        File.WriteAllBytes(servedPath, Encoding.UTF8.GetBytes("{\"Name\":\"T\"}"));

        var port = FindFreePort();
        using var server = new TournamentHttpServer();
        try
        {
            server.SetServedTournament(id, servedPath);
            server.Start(port);

            var target = MakeTarget();
            var mgr = new CapturingTournamentsManager { Response = MakeTarget() };
            var importer = new TournamentImporter(mgr, new List<IGroupBracketProcessor>());

            await importer.PrepareAsync(target, "http://127.0.0.1:" + port + "/tournament/" + id + ".wrt");

            File.Exists(mgr.LastLoadedPath).Should().BeFalse("temp file must be cleaned up after Prepare completes");
        }
        finally
        {
            server.Stop();
            File.Delete(servedPath);
        }
    }
}
