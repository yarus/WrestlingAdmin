using FluentAssertions;
using Wrestling.UI.Material.Model;
using Xunit;

namespace Wrestling.UI.Material.Tests;

// Verifies that two ImportSources entries pointing at the same physical
// peer (e.g. an old HTTP-only line and the same peer's freshly-announced
// "http+unc" packed line) are recognized as the same host. This is what
// lets AddDiscoveredPeer replace the stale entry in place instead of
// growing duplicate ListView lines for one laptop.
public sealed class PeerSourceMatcherTests
{
    [Theory]
    [InlineData("http://192.168.88.247:24566/tournament/abc.wrt", "192.168.88.247")]
    [InlineData("https://example.com/file.wrt", "example.com")]
    [InlineData("HTTP://Example.COM/file.wrt", "example.com")]
    [InlineData(@"\\192.168.88.247\Yarigin\20260426.wrt", "192.168.88.247")]
    [InlineData(@"\\HOST\share", "host")]
    [InlineData("//192.168.88.247/share/file", "192.168.88.247")]
    public void ExtractHosts_recognizes_single_candidate(string input, string expectedHost)
    {
        var hosts = PeerSourceMatcher.ExtractHosts(input);
        hosts.Should().BeEquivalentTo(new[] { expectedHost });
    }

    [Fact]
    public void ExtractHosts_returns_empty_for_garbage()
    {
        PeerSourceMatcher.ExtractHosts(null).Should().BeEmpty();
        PeerSourceMatcher.ExtractHosts("").Should().BeEmpty();
        PeerSourceMatcher.ExtractHosts("just-some-text").Should().BeEmpty();
        PeerSourceMatcher.ExtractHosts("ftp://nope").Should().BeEmpty();
    }

    [Fact]
    public void ExtractHosts_unfolds_packed_source_into_two_hosts()
    {
        var packed = "http://192.168.88.247:24566/tournament/abc.wrt|" +
                     @"\\192.168.88.247\Yarigin\file.wrt";
        var hosts = PeerSourceMatcher.ExtractHosts(packed);
        hosts.Should().BeEquivalentTo(new[] { "192.168.88.247" });
    }

    [Fact]
    public void ExtractHosts_packed_with_two_distinct_hosts_keeps_both()
    {
        var packed = "http://10.0.0.1/file.wrt|\\\\10.0.0.2\\share\\file.wrt";
        var hosts = PeerSourceMatcher.ExtractHosts(packed);
        hosts.Should().BeEquivalentTo(new[] { "10.0.0.1", "10.0.0.2" });
    }

    [Fact]
    public void SameHost_matches_http_only_against_packed_for_same_ip()
    {
        var httpOnly = "http://192.168.88.247:24566/tournament/abc.wrt";
        var packed = "http://192.168.88.247:24566/tournament/abc.wrt|" +
                     @"\\192.168.88.247\Yarigin\file.wrt";
        PeerSourceMatcher.SameHost(httpOnly, packed).Should().BeTrue();
        PeerSourceMatcher.SameHost(packed, httpOnly).Should().BeTrue();
    }

    [Fact]
    public void SameHost_matches_unc_only_against_packed_for_same_ip()
    {
        var uncOnly = @"\\192.168.88.247\Yarigin\file.wrt";
        var packed = "http://192.168.88.247:24566/tournament/abc.wrt|" +
                     @"\\192.168.88.247\Yarigin\file.wrt";
        PeerSourceMatcher.SameHost(uncOnly, packed).Should().BeTrue();
    }

    [Fact]
    public void SameHost_returns_false_for_different_ips()
    {
        var a = "http://192.168.88.247:24566/tournament/abc.wrt";
        var b = "http://192.168.88.249:24566/tournament/abc.wrt";
        PeerSourceMatcher.SameHost(a, b).Should().BeFalse();
    }

    [Fact]
    public void SameHost_handles_empty_inputs_safely()
    {
        PeerSourceMatcher.SameHost(null, "http://x").Should().BeFalse();
        PeerSourceMatcher.SameHost("http://x", null).Should().BeFalse();
        PeerSourceMatcher.SameHost("", "").Should().BeFalse();
    }
}
