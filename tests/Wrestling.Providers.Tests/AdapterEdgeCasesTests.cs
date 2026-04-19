using System;
using System.Collections.Generic;
using FluentAssertions;
using Wrestling.Data;
using Wrestling.Entities;
using Wrestling.Providers;
using Xunit;

namespace Wrestling.Providers.Tests;

public class AdapterEdgeCasesTests
{
    private readonly EntityToInfoAdapter _adapter = new();

    [Fact]
    public void GetEntityFromInfo_returns_null_for_null_input()
    {
        _adapter.GetEntityFromInfo((TournamentInfo)null).Should().BeNull();
        _adapter.GetEntityFromInfo((WrestlerInfo)null).Should().BeNull();
        _adapter.GetEntityFromInfo((TeamApplicationInfo)null).Should().BeNull();
    }

    [Fact]
    public void GetInfoFromEntity_returns_null_for_null_input()
    {
        _adapter.GetInfoFromEntity((Tournament)null).Should().BeNull();
        _adapter.GetInfoFromEntity((Wrestler)null).Should().BeNull();
        _adapter.GetInfoFromEntity((TeamApplication)null).Should().BeNull();
    }

    [Fact]
    public void Status_falls_back_to_Fake_when_empty_on_load()
    {
        var info = new TournamentInfo { ID = Guid.NewGuid(), Name = "N", Status = "", Settings = new GlobalSettingsInfo() };
        var entity = _adapter.GetEntityFromInfo(info);
        entity.Status.Should().Be(TournamentStatus.Fake);
    }

    // Bug-driver: unknown enum values in a saved file (schema drift) should not
    // crash the loader. Current code uses Enum.Parse and throws.
    [Fact]
    public void Status_unknown_value_does_not_crash_load()
    {
        var info = new TournamentInfo { ID = Guid.NewGuid(), Name = "N", Status = "SomethingRemovedInV2", Settings = new GlobalSettingsInfo() };

        Action load = () => _adapter.GetEntityFromInfo(info);

        load.Should().NotThrow("schema migration: unknown enums must not kill the whole load");
    }

    // Bug-driver: a wrestler whose TeamID can't be resolved is currently
    // deleted silently. Desired behavior is to keep the wrestler and clear
    // the dangling TeamID instead.
    [Fact]
    public void Wrestler_with_dangling_TeamID_is_retained_not_deleted()
    {
        var groupId = Guid.NewGuid();
        var wrestlerId = Guid.NewGuid();
        var missingTeamId = Guid.NewGuid();

        var info = new TournamentInfo
        {
            ID = Guid.NewGuid(),
            Name = "N",
            Settings = new GlobalSettingsInfo(),
            TeamApplications = new List<TeamApplicationInfo>(),
            Groups = new List<AgeWeightGroupInfo>
            {
                new AgeWeightGroupInfo
                {
                    ID = groupId,
                    BirthYearMin = 2005, BirthYearMax = 2006, WeightMax = 60,
                    Wrestlers = new List<Guid> { wrestlerId }
                }
            },
            Wrestlers = new List<WrestlerInfo>
            {
                new WrestlerInfo
                {
                    ID = wrestlerId,
                    FirstName = "Иван", LastName = "Иванов",
                    BirthDate = new DateTime(2005, 1, 1), Weight = 55,
                    IsEntryFeePaid = true, IsWeightApproved = true,
                    GroupID = groupId,
                    TeamID = missingTeamId
                }
            },
            Carpets = new List<CarpetInfo>(),
            Slides = new List<ScreenSlideInfo>(),
            ImportSources = new List<string>()
        };

        var entity = _adapter.GetEntityFromInfo(info);

        entity.Wrestlers.Should().HaveCount(1, "dangling team reference must not lose the wrestler");
        entity.Wrestlers[0].ID.Should().Be(wrestlerId);
        entity.Wrestlers[0].TeamID.Should().BeNull("the missing team link should be cleared");
    }
}
