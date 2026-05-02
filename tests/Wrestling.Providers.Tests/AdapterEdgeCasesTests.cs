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
            Slides = new List<ScreenSlideInfo>()
        };

        var entity = _adapter.GetEntityFromInfo(info);

        entity.Wrestlers.Should().HaveCount(1, "dangling team reference must not lose the wrestler");
        entity.Wrestlers[0].ID.Should().Be(wrestlerId);
        entity.Wrestlers[0].TeamID.Should().BeNull("the missing team link should be cleared");
    }

    // Pre-channel .wrt files serialize a flat `Slides` list on TournamentInfo.
    // The adapter must migrate it into a single default channel on load so old
    // tournaments don't lose their slides.
    [Fact]
    public void Legacy_flat_slides_migrate_into_one_default_channel()
    {
        var info = new TournamentInfo
        {
            ID = Guid.NewGuid(),
            Name = "N",
            Status = "Fake",
            Settings = new GlobalSettingsInfo(),
            Slides = new List<ScreenSlideInfo>
            {
                new ScreenSlideInfo { Title = "A", SlideType = "Image", Duration = 10 },
                new ScreenSlideInfo { Title = "B", SlideType = "Image", Duration = 15 }
            }
        };

        var entity = _adapter.GetEntityFromInfo(info);

        entity.SlideChannels.Should().HaveCount(1, "legacy flat slides collapse into one channel");
        entity.SlideChannels[0].Name.Should().Be("Основной");
        entity.SlideChannels[0].Slides.Should().HaveCount(2);
        entity.SlideChannels[0].Slides[0].Title.Should().Be("A");
    }

    // When a file already carries SlideChannels (post-migration), the legacy
    // Slides field on the DTO (if any) must be ignored — no duplicate import.
    [Fact]
    public void SlideChannels_take_precedence_over_legacy_Slides()
    {
        var info = new TournamentInfo
        {
            ID = Guid.NewGuid(),
            Name = "N",
            Status = "Fake",
            Settings = new GlobalSettingsInfo(),
            SlideChannels = new List<SlideChannelInfo>
            {
                new SlideChannelInfo { Name = "Канал 1", SliderMaxSecond = 20, Slides = new List<ScreenSlideInfo>() }
            },
            Slides = new List<ScreenSlideInfo>
            {
                new ScreenSlideInfo { Title = "stale", SlideType = "Image", Duration = 5 }
            }
        };

        var entity = _adapter.GetEntityFromInfo(info);

        entity.SlideChannels.Should().HaveCount(1);
        entity.SlideChannels[0].Name.Should().Be("Канал 1");
        entity.SlideChannels[0].Slides.Should().BeEmpty("legacy Slides must not re-populate existing channels");
    }

    // Round-trip: entity → info → entity keeps channel name, timer, and slides.
    [Fact]
    public void SlideChannel_round_trips_through_adapter()
    {
        var tournament = new Tournament(new GlobalSettings()) { ID = Guid.NewGuid(), Name = "N" };
        var channel = new SlideChannel
        {
            Name = "Главный",
            SliderMaxSecond = 30,
            Slides = new System.Collections.ObjectModel.ObservableCollection<ScreenSlide>
            {
                new ScreenSlide { Title = "S1", SlideType = "Image", Duration = 7 }
            }
        };
        tournament.SlideChannels.Add(channel);

        var info = _adapter.GetInfoFromEntity(tournament);
        var roundTripped = _adapter.GetEntityFromInfo(info);

        roundTripped.SlideChannels.Should().HaveCount(1);
        roundTripped.SlideChannels[0].Name.Should().Be("Главный");
        roundTripped.SlideChannels[0].SliderMaxSecond.Should().Be(30);
        roundTripped.SlideChannels[0].Slides.Should().HaveCount(1);
        roundTripped.SlideChannels[0].Slides[0].Title.Should().Be("S1");
    }
}
