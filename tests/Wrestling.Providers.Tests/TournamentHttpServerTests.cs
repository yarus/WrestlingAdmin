using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Wrestling.Providers.Network;
using Xunit;

namespace Wrestling.Providers.Tests;

public class TournamentHttpServerTests
{
    private static int FindFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static string WriteTempWrt(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), "wrthttp-" + Guid.NewGuid().ToString("N") + ".wrt");
        File.WriteAllText(path, content, Encoding.UTF8);
        return path;
    }

    [Fact]
    public async Task Valid_guid_returns_200_with_file_bytes()
    {
        var id = Guid.NewGuid();
        var path = WriteTempWrt("{\"Name\":\"Ярыгин 2025\"}");
        var port = FindFreePort();

        using var server = new TournamentHttpServer();
        try
        {
            server.SetServedTournament(id, path);
            server.Start(port);
            server.ActualPort.Should().Be(port);

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var resp = await http.GetAsync("http://127.0.0.1:" + port + "/tournament/" + id + ".wrt");

            resp.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await resp.Content.ReadAsStringAsync();
            body.Should().Be("{\"Name\":\"Ярыгин 2025\"}");
        }
        finally
        {
            server.Stop();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Wrong_guid_returns_404()
    {
        var servedId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var path = WriteTempWrt("payload");
        var port = FindFreePort();

        using var server = new TournamentHttpServer();
        try
        {
            server.SetServedTournament(servedId, path);
            server.Start(port);

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var resp = await http.GetAsync("http://127.0.0.1:" + port + "/tournament/" + otherId + ".wrt");

            resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        finally
        {
            server.Stop();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Missing_file_returns_404()
    {
        var id = Guid.NewGuid();
        var port = FindFreePort();

        using var server = new TournamentHttpServer();
        try
        {
            server.SetServedTournament(id, @"C:\does\not\exist\missing.wrt");
            server.Start(port);

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var resp = await http.GetAsync("http://127.0.0.1:" + port + "/tournament/" + id + ".wrt");

            resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        finally
        {
            server.Stop();
        }
    }

    [Fact]
    public async Task Malformed_path_returns_404()
    {
        var port = FindFreePort();
        using var server = new TournamentHttpServer();
        try
        {
            server.SetServedTournament(Guid.NewGuid(), WriteTempWrt("x"));
            server.Start(port);

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var resp = await http.GetAsync("http://127.0.0.1:" + port + "/not-a-tournament-url");

            resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        finally
        {
            server.Stop();
        }
    }

    [Fact]
    public async Task Non_GET_method_returns_405()
    {
        var id = Guid.NewGuid();
        var path = WriteTempWrt("x");
        var port = FindFreePort();

        using var server = new TournamentHttpServer();
        try
        {
            server.SetServedTournament(id, path);
            server.Start(port);

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var resp = await http.PostAsync(
                "http://127.0.0.1:" + port + "/tournament/" + id + ".wrt",
                new StringContent("body"));

            resp.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
        }
        finally
        {
            server.Stop();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Concurrent_requests_all_complete_successfully()
    {
        var id = Guid.NewGuid();
        var contentBytes = new byte[64 * 1024];
        new Random(42).NextBytes(contentBytes);
        var path = Path.Combine(Path.GetTempPath(), "wrthttp-concurrent-" + Guid.NewGuid().ToString("N") + ".wrt");
        File.WriteAllBytes(path, contentBytes);
        var port = FindFreePort();

        using var server = new TournamentHttpServer();
        try
        {
            server.SetServedTournament(id, path);
            server.Start(port);

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var url = "http://127.0.0.1:" + port + "/tournament/" + id + ".wrt";
            var tasks = new Task<byte[]>[10];
            for (int i = 0; i < tasks.Length; i++)
            {
                tasks[i] = http.GetByteArrayAsync(url);
            }
            var results = await Task.WhenAll(tasks);

            foreach (var result in results)
            {
                result.Should().Equal(contentBytes);
            }
        }
        finally
        {
            server.Stop();
            File.Delete(path);
        }
    }

    [Fact]
    public void Port_conflict_raises_diagnostic_and_keeps_ActualPort_null()
    {
        var port = FindFreePort();
        var blockerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        blockerSocket.ExclusiveAddressUse = true;
        blockerSocket.Bind(new IPEndPoint(IPAddress.Any, port));
        blockerSocket.Listen(1);

        try
        {
            using var server = new TournamentHttpServer();
            string diagnostic = null;
            server.DiagnosticMessage += (s, m) => diagnostic = m;

            server.Start(port);

            server.ActualPort.Should().BeNull();
            diagnostic.Should().NotBeNull().And.Contain(port.ToString());
        }
        finally
        {
            blockerSocket.Close();
        }
    }
}
