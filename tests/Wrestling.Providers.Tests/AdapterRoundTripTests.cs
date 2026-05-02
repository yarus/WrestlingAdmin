using System;
using System.Collections.ObjectModel;
using System.Linq;
using FluentAssertions;
using Wrestling.Data;
using Wrestling.Entities;
using Wrestling.Entities.Bracket;
using Wrestling.Providers;
using Xunit;

namespace Wrestling.Providers.Tests;

public class AdapterRoundTripTests
{
    private readonly EntityToInfoAdapter _adapter = new();

    [Fact]
    public void Round_trip_of_simple_tournament_preserves_scalar_fields()
    {
        var t = new Tournament(new GlobalSettings())
        {
            ID = Guid.NewGuid(),
            Name = "Cup",
            City = "Москва",
            Country = "RU",
            Address = "ул. Ленина 1",
            HashTag = "#cup",
            MainJudge = "Judge",
            MainJudgeEmail = "j@j.r",
            MainJudgePhone = "+7",
            MainSecretary = "Sec",
            MainSecretaryEmail = "s@s.r",
            MainSecretaryPhone = "+71",
            EntryFee = 100m,
            StartDate = new DateTime(2026, 5, 1),
            Status = TournamentStatus.InProgress
        };

        var info = _adapter.GetInfoFromEntity(t);
        var restored = _adapter.GetEntityFromInfo(info);

        restored.Should().NotBeNull();
        restored.ID.Should().Be(t.ID);
        restored.Name.Should().Be(t.Name);
        restored.City.Should().Be(t.City);
        restored.Country.Should().Be(t.Country);
        restored.Address.Should().Be(t.Address);
        restored.HashTag.Should().Be(t.HashTag);
        restored.MainJudge.Should().Be(t.MainJudge);
        restored.MainJudgeEmail.Should().Be(t.MainJudgeEmail);
        restored.MainSecretaryEmail.Should().Be(t.MainSecretaryEmail);
        restored.EntryFee.Should().Be(100m);
        restored.StartDate.Should().Be(new DateTime(2026, 5, 1));
        restored.Status.Should().Be(TournamentStatus.InProgress);
    }

    [Fact]
    public void Round_trip_preserves_GlobalSettings_including_video_storage_path()
    {
        var settings = new GlobalSettings
        {
            MaxRoundSecond = 120,
            MaxTimeoutSecond = 30,
            MaxActionSecond = 15,
            IsTimerBackward = true,
            IsSoundEnabled = false,
            IsAutosaveEnabled = true,
            IsOverlayOlympic = false,
            IsBackupEnabled = false,
            MaxBackupCount = 7,
            BackupFolderPath = @"D:\shared-backups"
        };
        var t = new Tournament(settings) { ID = Guid.NewGuid(), Name = "N" };

        var info = _adapter.GetInfoFromEntity(t);
        var restored = _adapter.GetEntityFromInfo(info);

        restored.Settings.MaxRoundSecond.Should().Be(120);
        restored.Settings.MaxActionSecond.Should().Be(15);
        restored.Settings.IsTimerBackward.Should().BeTrue();
        restored.Settings.IsSoundEnabled.Should().BeFalse();
        restored.Settings.IsAutosaveEnabled.Should().BeTrue();
        restored.Settings.IsBackupEnabled.Should().BeFalse();
        restored.Settings.MaxBackupCount.Should().Be(7);
        restored.Settings.BackupFolderPath.Should().Be(@"D:\shared-backups");
    }

    [Fact]
    public void Legacy_info_without_backup_fields_applies_safe_defaults()
    {
        // Simulates a .wrt saved before the backup feature: the persisted
        // MaxBackupCount is missing (0) and IsBackupEnabled is missing.
        // The info constructor seeds safe defaults so Newtonsoft loads the
        // pre-feature JSON with backups enabled.
        var legacyInfo = new Wrestling.Data.TournamentInfo
        {
            ID = Guid.NewGuid(),
            Name = "legacy",
            Settings = new Wrestling.Data.GlobalSettingsInfo { MaxBackupCount = 0 }
        };

        var restored = _adapter.GetEntityFromInfo(legacyInfo);

        restored.Settings.IsBackupEnabled.Should().BeTrue("safe default must enable backups for legacy files");
        restored.Settings.MaxBackupCount.Should().Be(10, "zero is normalized to the default retention");
    }

    [Fact]
    public void Round_trip_preserves_wrestlers_with_group_membership()
    {
        var group = new AgeWeightGroup
        {
            ID = Guid.NewGuid(),
            BirthYearMin = 2005,
            BirthYearMax = 2006,
            WeightMax = 70,
            MaxRoundSecond = 180
        };

        var wrestler = new Wrestler
        {
            ID = Guid.NewGuid(),
            FirstName = "Иван",
            LastName = "Иванов",
            BirthDate = new DateTime(2005, 3, 1),
            Weight = 68,
            IsEntryFeePaid = true,
            IsWeightApproved = true,
            GroupID = group.ID
        };
        group.Wrestlers = new System.Collections.Generic.List<Wrestler> { wrestler };

        var t = new Tournament(new GlobalSettings()) { ID = Guid.NewGuid(), Name = "T" };
        t.Groups.Add(group);
        t.Wrestlers.Add(wrestler);

        var info = _adapter.GetInfoFromEntity(t);
        var restored = _adapter.GetEntityFromInfo(info);

        restored.Wrestlers.Should().HaveCount(1);
        var w = restored.Wrestlers[0];
        w.ID.Should().Be(wrestler.ID);
        w.FirstName.Should().Be("Иван");
        w.GroupID.Should().Be(group.ID);
        w.GroupName.Should().Be(group.Name);

        restored.Groups.Should().HaveCount(1);
        restored.Groups[0].Wrestlers.Should().Contain(x => x.ID == wrestler.ID);
    }

    [Fact]
    public void Round_trip_assigns_a_new_Guid_when_tournament_has_none()
    {
        var t = new Tournament(new GlobalSettings()) { Name = "No-ID" };
        var info = _adapter.GetInfoFromEntity(t);

        info.ID.Should().NotBeNull();
        t.ID.Should().Be(info.ID, "the entity is mutated to carry the new Guid");
    }

    [Fact]
    public void Round_trip_preserves_network_settings()
    {
        var settings = new GlobalSettings
        {
            DiscoveryPort = 40000,
            IsHttpServerEnabled = false,
            HttpServerPort = 40001,
            NodeName = "Ковёр 3",
            SelfUncPath = @"\\HOST\Share\tournament.wrt",
            AnnounceIpOverride = "192.168.88.42"
        };
        var t = new Tournament(settings) { ID = Guid.NewGuid(), Name = "N" };

        var info = _adapter.GetInfoFromEntity(t);
        var restored = _adapter.GetEntityFromInfo(info);

        restored.Settings.DiscoveryPort.Should().Be(40000);
        restored.Settings.IsHttpServerEnabled.Should().BeFalse();
        restored.Settings.HttpServerPort.Should().Be(40001);
        restored.Settings.NodeName.Should().Be("Ковёр 3");
        restored.Settings.SelfUncPath.Should().Be(@"\\HOST\Share\tournament.wrt");
        restored.Settings.AnnounceIpOverride.Should().Be("192.168.88.42");
    }

    [Fact]
    public void Legacy_info_without_network_fields_applies_safe_defaults()
    {
        // Simulates a .wrt saved before the discovery feature: the persisted
        // DiscoveryPort/HttpServerPort are missing (0), NodeName absent.
        // The info constructor seeds defaults, the adapter normalizes zero-ports
        // and falls back to MachineName for an empty NodeName so the laptop is
        // discoverable out of the box.
        var legacyInfo = new Wrestling.Data.TournamentInfo
        {
            ID = Guid.NewGuid(),
            Name = "legacy",
            Settings = new Wrestling.Data.GlobalSettingsInfo { DiscoveryPort = 0, HttpServerPort = 0 }
        };

        var restored = _adapter.GetEntityFromInfo(legacyInfo);

        restored.Settings.DiscoveryPort.Should().Be(24565);
        restored.Settings.IsHttpServerEnabled.Should().BeTrue();
        restored.Settings.HttpServerPort.Should().Be(24566);
        restored.Settings.NodeName.Should().Be(Environment.MachineName);
        restored.Settings.SelfUncPath.Should().BeEmpty();
        restored.Settings.AnnounceIpOverride.Should().BeEmpty();
    }

    [Fact]
    public void Round_trip_of_bracket_preserves_match_state_and_winner_reference()
    {
        var group = new AgeWeightGroup
        {
            ID = Guid.NewGuid(),
            BirthYearMin = 2005, BirthYearMax = 2006, WeightMax = 70,
            MaxRoundSecond = 180, MaxTimeoutSecond = 30, MaxActionSecond = 30
        };
        var wrestlers = Enumerable.Range(0, 4).Select(i => new Wrestler
        {
            ID = Guid.NewGuid(),
            FirstName = $"W{i}",
            LastName = $"Фамилия{i}",
            BirthDate = new DateTime(2005, 1, 1),
            Weight = 68,
            IsEntryFeePaid = true,
            IsWeightApproved = true,
            GroupID = group.ID,
            SeedNumber = i + 1
        }).ToList();

        group.Wrestlers = wrestlers;
        var t = new Tournament(new GlobalSettings()) { ID = Guid.NewGuid(), Name = "T" };
        t.Groups.Add(group);
        foreach (var w in wrestlers) t.Wrestlers.Add(w);

        var proc = new OlympicGroupBracketProcessor();
        proc.Generate(t, group);

        var semiA = group.Bracket.Rounds[0].RoundMatches[0];
        proc.CompleteMatch(semiA, isRedWon: true, MatchWinTypeEnum.PointsWin);

        var info = _adapter.GetInfoFromEntity(t);
        var restored = _adapter.GetEntityFromInfo(info);
        var restoredGroup = restored.Groups[0];
        var restoredSemi = restoredGroup.Bracket.Rounds[0].RoundMatches[0];
        var restoredFinal = restoredGroup.Bracket.Rounds[1].RoundMatches[0];

        restoredSemi.Status.Should().Be(MatchStatusEnum.Completed);
        restoredSemi.WinType.Should().Be(MatchWinTypeEnum.PointsWin);
        restoredSemi.IsRedWon.Should().BeTrue();

        // Critical: winner reference identity is preserved through Id lookup
        var winnerId = semiA.WrestlerInRed!.ID;
        (restoredFinal.WrestlerInRed?.ID == winnerId || restoredFinal.WrestlerInBlue?.ID == winnerId)
            .Should().BeTrue();
    }
}
