using System;
using FluentAssertions;
using Wrestling.UI.Material.Model;
using Xunit;
using static Wrestling.UI.Material.Model.WindowsFirewallAccessProbe;

namespace Wrestling.UI.Material.Tests;

public sealed class WindowsFirewallAccessProbeTests
{
    [Fact]
    public void No_permission_in_default_block_profile_is_not_allowed()
        => Evaluate(Array.Empty<Rule>(), 2, 17, 24565).Should().Be(FirewallAccess.NotAllowed);

    [Theory]
    [InlineData(2, 17, "*", 0)]
    [InlineData(4, 17, "*", 1)]
    [InlineData(2, 6, "*", 1)]
    [InlineData(2, 17, "24566", 1)]
    [InlineData(2, 17, "100,24560-24570", 0)]
    [InlineData(2, 256, "*", 0)]
    [InlineData(2, 17, "RPC", 2)]
    public void Permission_must_cover_current_profile_protocol_and_port(int profiles, int protocol,
        string ports, int expected)
        => Evaluate(new[] { new Rule(profiles, protocol, ports, true, false) }, 2, 17, 24565).Should().Be((FirewallAccess)expected);

    [Fact]
    public void Explicit_block_overrides_allow()
        => Evaluate(new[] { new Rule(2, 17, "*", true, false), new Rule(2, 17, "*", false, false) },
            2, 17, 24565).Should().Be(FirewallAccess.NotAllowed);

    [Fact]
    public void Scoped_allow_cannot_prove_permission_for_unknown_peers()
        => Evaluate(new[] { new Rule(2, 17, "*", true, true) }, 2, 17, 24565).Should().Be(FirewallAccess.Unknown);

    [Fact]
    public void Scoped_block_cannot_be_ignored_even_with_allow()
        => Evaluate(new[] { new Rule(2, 17, "*", true, false), new Rule(2, 17, "*", false, true) },
            2, 17, 24565).Should().Be(FirewallAccess.Unknown);

    [Fact]
    public void Disabled_firewall_needs_no_exception()
        => Evaluate(Array.Empty<Rule>(), 2, 17, 24565, enabled: false).Should().Be(FirewallAccess.Allowed);

    [Fact]
    public void Block_all_overrides_exception()
        => Evaluate(new[] { new Rule(2, 17, "*", true, false) }, 2, 17, 24565, blockAll: true)
            .Should().Be(FirewallAccess.NotAllowed);

    [Fact]
    public void Default_allow_needs_no_exception()
        => Evaluate(Array.Empty<Rule>(), 2, 17, 24565, defaultAllow: true).Should().Be(FirewallAccess.Allowed);

    [Fact]
    public void Different_active_profiles_cannot_prove_permission_on_tournament_network()
        => Combine(new[] { FirewallAccess.Allowed, FirewallAccess.NotAllowed }).Should().Be(FirewallAccess.Unknown);

    [Fact]
    public void Http_block_is_reported_even_when_discovery_is_allowed()
        => CombinePorts(FirewallAccess.Allowed, FirewallAccess.NotAllowed).Should().Be(FirewallAccess.NotAllowed);
}
