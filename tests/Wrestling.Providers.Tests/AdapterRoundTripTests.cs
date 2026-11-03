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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Round_robin_bronze_policy_round_trips_without_changing_legacy_default(bool enabled)
    {
        var tournament = new Tournament(new GlobalSettings { IsRoundRobinTwoThirdPlaces = enabled });
        var info = _adapter.GetInfoFromEntity(tournament);
        info.Settings.IsRoundRobinTwoThirdPlaces.Should().Be(enabled);
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(info);
        var loaded = Newtonsoft.Json.JsonConvert.DeserializeObject<TournamentInfo>(json);
        _adapter.GetEntityFromInfo(loaded).Settings.IsRoundRobinTwoThirdPlaces.Should().Be(enabled);
        var legacySettings = Newtonsoft.Json.JsonConvert.DeserializeObject<GlobalSettingsInfo>("{}");
        _adapter.GetEntityFromInfo(legacySettings).IsRoundRobinTwoThirdPlaces.Should().BeFalse();
    }

    [Fact]
    public void Legacy_resolved_branch_is_normalized_on_load_without_version_changes()
    {
        var group = new AgeWeightGroup { ID = Guid.NewGuid() };
        var tournament = new Tournament(new GlobalSettings()) { ID = Guid.NewGuid() };
        tournament.Groups.Add(group);
        for (var i = 0; i < 8; i++)
        {
            var wrestler = new Wrestler { ID = Guid.NewGuid(), GroupID = group.ID, SeedNumber = i + 1 };
            group.Wrestlers.Add(wrestler); tournament.Wrestlers.Add(wrestler);
        }
        var processor = new OlympicWithConsolationFromFinalistsGroupBracketProcessor();
        processor.Generate(tournament, group);
        processor.CompleteMatch(group.Bracket.MainRounds().First().RoundMatches.First(), null, MatchWinTypeEnum.MutualNoShow);
        for (var step = 0; step < 30; step++)
        {
            var next = group.Bracket.Rounds.SelectMany(r => r.RoundMatches).FirstOrDefault(m => m.IsMatchCanStart);
            if (next == null) break;
            processor.CompleteMatch(next, true, MatchWinTypeEnum.PointsWin);
        }
        var bronze = processor.Get3rdPlaceRound(group).RoundMatches.First(m => m.WinType == MatchWinTypeEnum.FreeWin);
        bronze.Status = MatchStatusEnum.Pending; bronze.WinType = null; bronze.IsRedWon = null;
        bronze.Version = 3;
        var bracketVersion = group.BracketVersion;
        processor.Load(tournament, group);
        processor.GetResults().ToList();
        bronze.Status.Should().Be(MatchStatusEnum.Pending, "queries do not perform legacy recovery");

        var restored = _adapter.GetEntityFromInfo(_adapter.GetInfoFromEntity(tournament));
        var normalized = restored.Groups[0].Bracket.Rounds.SelectMany(r => r.RoundMatches).Single(m => m.BracketFullNumber == bronze.BracketFullNumber);
        normalized.Status.Should().Be(MatchStatusEnum.Completed);
        normalized.WinType.Should().Be(MatchWinTypeEnum.FreeWin);
        normalized.Version.Should().Be(3);
        restored.Groups[0].BracketVersion.Should().Be(bracketVersion);
        restored.Groups[0].Bracket.CompletedMatchesCount.Should().Be(restored.Groups[0].Bracket.MatchesCount);
    }

    [Fact]
    public void Round_trip_preserves_mat_group_order_independent_of_tournament_group_order()
    {
        var tournament = new Tournament(new GlobalSettings()) { ID = Guid.NewGuid() };
        var first = new AgeWeightGroup { ID = Guid.NewGuid() };
        var second = new AgeWeightGroup { ID = Guid.NewGuid() };
        tournament.Groups.Add(first);
        tournament.Groups.Add(second);
        var mat = new Mat { ID = Guid.NewGuid(), Groups = new ObservableCollection<AgeWeightGroup> { second, first } };
        tournament.Mats.Add(mat);

        var restored = _adapter.GetEntityFromInfo(_adapter.GetInfoFromEntity(tournament));

        restored.Mats[0].Groups.Select(g => g.ID).Should().Equal(second.ID, first.ID);
        restored.Mats[0].Groups[0].Should().BeSameAs(restored.Groups[1]);
    }

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
        restored.Settings.MaxBackupCount.Should().Be(20, "zero is normalized to the default retention");
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
    public void Round_trip_preserves_IsDisqualified_flag_on_wrestler()
    {
        var group = new AgeWeightGroup { ID = Guid.NewGuid(), BirthYearMin = 2005, BirthYearMax = 2006, WeightMax = 60, MaxRoundSecond = 180 };
        var w = new Wrestler
        {
            ID = Guid.NewGuid(),
            FirstName = "Иван",
            LastName = "Иванов",
            BirthDate = new DateTime(2005, 3, 1),
            GroupID = group.ID,
            IsDisqualified = true
        };
        group.Wrestlers = new System.Collections.Generic.List<Wrestler> { w };
        var t = new Tournament(new GlobalSettings()) { ID = Guid.NewGuid(), Name = "T" };
        t.Groups.Add(group);
        t.Wrestlers.Add(w);

        var info = _adapter.GetInfoFromEntity(t);
        var restored = _adapter.GetEntityFromInfo(info);

        restored.Wrestlers[0].IsDisqualified.Should().BeTrue();
    }

    [Fact]
    public void Legacy_wrt_without_IsDisqualified_field_loads_as_false()
    {
        // Simulate a DTO that came from old JSON: IsDisqualified defaults to
        // false (Newtonsoft fills missing fields from the parameterless ctor
        // / default(bool)). The adapter must not require the field to be set.
        var info = new TournamentInfo
        {
            ID = Guid.NewGuid(),
            Name = "Legacy",
            Groups = new ObservableCollection<AgeWeightGroupInfo>(),
            Wrestlers = new ObservableCollection<WrestlerInfo>
            {
                new WrestlerInfo
                {
                    ID = Guid.NewGuid(),
                    FirstName = "Иван",
                    LastName = "Иванов",
                    BirthDate = new DateTime(2005, 3, 1)
                    // IsDisqualified not set — defaults to false
                }
            },
            TeamApplications = new ObservableCollection<TeamApplicationInfo>(),
            Settings = new GlobalSettingsInfo()
        };

        var restored = _adapter.GetEntityFromInfo(info);
        restored.Wrestlers[0].IsDisqualified.Should().BeFalse();
    }

    [Fact]
    public void Round_trip_assigns_a_new_Guid_when_tournament_has_none()
    {
        var t = new Tournament(new GlobalSettings()) { Name = "No-ID" };
        var info = _adapter.GetInfoFromEntity(t);

        info.ID.Should().NotBeNull();
        t.ID.Should().Be(info.ID, "the entity is mutated to carry the new Guid");
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"NodeName\":\"Ковёр чужого ПК\",\"DiscoveryPort\":40000,\"HttpServerPort\":40001,\"IsHttpServerEnabled\":false,\"AnnounceIpOverride\":\"192.168.88.42\"}")]
    public void Legacy_network_fields_are_ignored_and_not_written_into_copied_tournament(string settingsJson)
    {
        var info = new TournamentInfo
        {
            ID = Guid.NewGuid(), Name = "Copied cup",
            Settings = Newtonsoft.Json.JsonConvert.DeserializeObject<GlobalSettingsInfo>(settingsJson)
        };
        var restored = _adapter.GetEntityFromInfo(info);
        restored.ID.Should().Be(info.ID);
        var saved = Newtonsoft.Json.Linq.JObject.FromObject(_adapter.GetInfoFromEntity(restored));
        var settings = (Newtonsoft.Json.Linq.JObject)saved["Settings"];
        foreach (var key in new[] { "NodeName", "DiscoveryPort", "HttpServerPort", "IsHttpServerEnabled", "AnnounceIpOverride" })
            settings.Property(key).Should().BeNull();
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
