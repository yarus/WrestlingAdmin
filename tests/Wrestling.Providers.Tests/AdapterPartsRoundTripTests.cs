using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using FluentAssertions;
using Wrestling.Data;
using Wrestling.Entities;
using Wrestling.Providers;
using Xunit;

namespace Wrestling.Providers.Tests;

public class AdapterPartsRoundTripTests
{
    [Fact]
    public void Legacy_info_without_parts_gets_default_part_assigned_to_groups_and_mats()
    {
        // Simulates an old .wrt file: TournamentInfo with groups and mats
        // but no Parts collection. Adapter must materialise a default part
        // and back-fill PartID/ActivePartID so downstream code can assume
        // the invariants.
        var matId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var info = new TournamentInfo
        {
            ID = Guid.NewGuid(),
            Name = "Legacy",
            Mats = new[] { new MatInfo { ID = matId, Name = "Ковёр 1", Groups = new[] { groupId } } },
            Groups = new[]
            {
                new AgeWeightGroupInfo
                {
                    ID = groupId,
                    MatID = matId,
                    Wrestlers = new List<Guid>(),
                    BirthYearMin = 2010, BirthYearMax = 2011, WeightMax = 50,
                    MaxRoundSecond = 180, MaxTimeoutSecond = 30, MaxActionSecond = 30
                }
            },
            Wrestlers = new List<WrestlerInfo>(),
            TeamApplications = new List<TeamApplicationInfo>(),
            Settings = new GlobalSettingsInfo { MaxBackupCount = 10 }
        };

        var entity = new EntityToInfoAdapter().GetEntityFromInfo(info);

        entity.Parts.Should().HaveCount(1, "adapter must create default part on legacy load");
        var defaultPart = entity.Parts[0];
        defaultPart.Name.Should().NotBeNullOrEmpty();

        entity.Groups.Single().PartID.Should().Be(defaultPart.ID, "group inherits default part");
        entity.Mats.Single().ActivePartID.Should().Be(defaultPart.ID, "mat inherits default part as active");
    }

    [Fact]
    public void Existing_parts_round_trip_intact()
    {
        var partA = new TournamentPart { ID = Guid.NewGuid(), Name = "Утро" };
        var partB = new TournamentPart { ID = Guid.NewGuid(), Name = "Вечер" };
        var matId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var t = new Tournament(new GlobalSettings()) { ID = Guid.NewGuid(), Name = "T", MetaVersion = 7 };
        t.Parts.Add(partA);
        t.Parts.Add(partB);
        var mat = new Mat { ID = matId, Name = "M1", ActivePartID = partB.ID, FieldsVersion = 3 };
        var group = new AgeWeightGroup
        {
            ID = groupId, PartID = partA.ID, MatID = matId,
            BirthYearMin = 2010, BirthYearMax = 2011, WeightMax = 50,
            MaxRoundSecond = 180, MaxTimeoutSecond = 30, MaxActionSecond = 30
        };
        t.Groups.Add(group);
        mat.Groups.Add(group);
        t.Mats.Add(mat);

        var adapter = new EntityToInfoAdapter();
        var info = adapter.GetInfoFromEntity(t);
        var roundTripped = adapter.GetEntityFromInfo(info);

        roundTripped.Parts.Should().HaveCount(2);
        roundTripped.Parts.Select(p => p.Name).Should().ContainInOrder("Утро", "Вечер");
        roundTripped.MetaVersion.Should().Be(7);
        roundTripped.Mats.Single().ActivePartID.Should().Be(partB.ID);
        roundTripped.Mats.Single().FieldsVersion.Should().Be(3);
        roundTripped.Groups.Single().PartID.Should().Be(partA.ID);
    }

    [Fact]
    public void Orphan_PartID_pointing_at_deleted_part_resnaps_to_first_part_on_load()
    {
        var goodPart = new TournamentPart { ID = Guid.NewGuid(), Name = "Часть 1" };
        var ghostPartId = Guid.NewGuid(); // doesn't exist on the loaded tournament

        var info = new TournamentInfo
        {
            ID = Guid.NewGuid(),
            Name = "Defensive",
            Parts = new[] { new TournamentPartInfo { ID = goodPart.ID, Name = goodPart.Name } },
            Mats = new[]
            {
                new MatInfo { ID = Guid.NewGuid(), Name = "M1", Groups = Array.Empty<Guid>(),
                    ActivePartID = ghostPartId }
            },
            Groups = new[]
            {
                new AgeWeightGroupInfo
                {
                    ID = Guid.NewGuid(),
                    PartID = ghostPartId,
                    Wrestlers = new List<Guid>(),
                    BirthYearMin = 2010, BirthYearMax = 2011, WeightMax = 50,
                    MaxRoundSecond = 180, MaxTimeoutSecond = 30, MaxActionSecond = 30
                }
            },
            Wrestlers = new List<WrestlerInfo>(),
            TeamApplications = new List<TeamApplicationInfo>(),
            Settings = new GlobalSettingsInfo { MaxBackupCount = 10 }
        };

        var entity = new EntityToInfoAdapter().GetEntityFromInfo(info);

        entity.Mats.Single().ActivePartID.Should().Be(goodPart.ID, "ghost ActivePartID resnaps to first part");
        entity.Groups.Single().PartID.Should().Be(goodPart.ID, "ghost PartID resnaps to first part");
    }
}
