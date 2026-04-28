using System.Net;
using FluentAssertions;
using Wrestling.Providers.Network;
using Xunit;

namespace Wrestling.Providers.Tests;

// Verifies the override path of LocalIpAddressProbe.PickAnnounceAddress —
// the auto-selection branch is enumerated from the host's NICs and would
// be flaky to assert on directly, so we focus on the four ways an override
// can be rejected and the one way it can be honored.
public sealed class LocalIpAddressProbeTests
{
    [Fact]
    public void Empty_override_returns_PickDefault()
    {
        var auto = LocalIpAddressProbe.PickDefault();
        LocalIpAddressProbe.PickAnnounceAddress(null).Should().Be(auto);
        LocalIpAddressProbe.PickAnnounceAddress("").Should().Be(auto);
        LocalIpAddressProbe.PickAnnounceAddress("   ").Should().Be(auto);
    }

    [Fact]
    public void Garbage_override_falls_back_to_auto()
    {
        var auto = LocalIpAddressProbe.PickDefault();
        LocalIpAddressProbe.PickAnnounceAddress("not-an-ip").Should().Be(auto);
        LocalIpAddressProbe.PickAnnounceAddress("999.999.999.999").Should().Be(auto);
    }

    [Fact]
    public void Loopback_override_is_rejected()
    {
        var auto = LocalIpAddressProbe.PickDefault();
        LocalIpAddressProbe.PickAnnounceAddress("127.0.0.1").Should().Be(auto);
    }

    [Fact]
    public void Override_for_address_not_on_machine_falls_back_to_auto()
    {
        // 203.0.113.0/24 is the TEST-NET-3 reserved range — guaranteed not
        // to be assigned to any real interface anywhere.
        var auto = LocalIpAddressProbe.PickDefault();
        LocalIpAddressProbe.PickAnnounceAddress("203.0.113.99").Should().Be(auto);
    }

    [Fact]
    public void Override_matching_a_real_machine_address_is_honored()
    {
        var lan = LocalIpAddressProbe.EnumerateLanAddresses();
        if (lan.Count == 0)
        {
            // Headless CI without any non-loopback IPv4 — nothing to honor;
            // skip rather than fail because the path under test cannot fire.
            return;
        }
        var pinned = lan[0];
        LocalIpAddressProbe.PickAnnounceAddress(pinned.ToString()).Should().Be(pinned);
    }
}
