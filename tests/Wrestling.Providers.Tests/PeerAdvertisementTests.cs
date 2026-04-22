using System;
using System.Text;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using Wrestling.Providers.Network;
using Xunit;

namespace Wrestling.Providers.Tests;

public class PeerAdvertisementTests
{
    [Fact]
    public void Round_trip_preserves_all_fields_including_cyrillic()
    {
        var original = new PeerAdvertisement
        {
            Proto = PeerAdvertisement.CurrentProto,
            InstanceId = Guid.NewGuid(),
            TournamentId = Guid.NewGuid(),
            TournamentTitle = "Ярыгин 2025",
            NodeName = "Ковёр 1",
            HttpUrl = "http://192.168.1.50:24566/tournament/deadbeef-dead-beef-dead-beefdeadbeef.wrt",
            UncPath = @"\\192.168.1.50\TShare\Ярыгин.wrt",
            AppVersion = "1.2.3",
            SentAt = new DateTime(2026, 4, 21, 12, 34, 56, DateTimeKind.Utc)
        };

        var bytes = original.ToBytes();
        var restored = PeerAdvertisement.TryFromBytes(bytes);

        restored.Should().NotBeNull();
        restored!.Proto.Should().Be(original.Proto);
        restored.InstanceId.Should().Be(original.InstanceId);
        restored.TournamentId.Should().Be(original.TournamentId);
        restored.TournamentTitle.Should().Be("Ярыгин 2025");
        restored.NodeName.Should().Be("Ковёр 1");
        restored.HttpUrl.Should().Be(original.HttpUrl);
        restored.UncPath.Should().Be(original.UncPath);
        restored.AppVersion.Should().Be("1.2.3");
    }

    [Fact]
    public void Wire_format_uses_documented_JSON_field_names()
    {
        var ad = new PeerAdvertisement
        {
            InstanceId = Guid.NewGuid(),
            TournamentId = Guid.NewGuid(),
            TournamentTitle = "T",
            NodeName = "N",
            HttpUrl = "h",
            UncPath = "u",
            AppVersion = "v"
        };
        var json = Encoding.UTF8.GetString(ad.ToBytes());
        var parsed = JObject.Parse(json);

        parsed["proto"].Should().NotBeNull();
        parsed["instanceId"].Should().NotBeNull();
        parsed["tournamentId"].Should().NotBeNull();
        parsed["tournamentTitle"].Should().NotBeNull();
        parsed["nodeName"].Should().NotBeNull();
        parsed["httpUrl"].Should().NotBeNull();
        parsed["uncPath"].Should().NotBeNull();
        parsed["appVersion"].Should().NotBeNull();
        parsed["sentAt"].Should().NotBeNull();
    }

    [Fact]
    public void Unknown_JSON_fields_are_tolerated()
    {
        const string json = @"{""proto"":1,""instanceId"":""00000000-0000-0000-0000-000000000001"",""tournamentId"":""00000000-0000-0000-0000-000000000002"",""nodeName"":""N"",""futureFeatureX"":true,""anotherUnknown"":[1,2,3]}";
        var ad = PeerAdvertisement.TryFromBytes(Encoding.UTF8.GetBytes(json));

        ad.Should().NotBeNull();
        ad!.NodeName.Should().Be("N");
    }

    [Fact]
    public void Garbage_bytes_parse_to_null_without_throwing()
    {
        PeerAdvertisement.TryFromBytes(new byte[] { 0x00, 0xFF, 0x42 }).Should().BeNull();
        PeerAdvertisement.TryFromBytes(Array.Empty<byte>()).Should().BeNull();
        PeerAdvertisement.TryFromBytes(null).Should().BeNull();
        PeerAdvertisement.TryFromBytes(Encoding.UTF8.GetBytes("not json")).Should().BeNull();
    }
}
