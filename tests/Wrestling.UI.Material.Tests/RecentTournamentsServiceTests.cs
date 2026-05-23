using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Wrestling.UI.Material.Home;
using Wrestling.UI.Material.Theme;
using Xunit;

namespace Wrestling.UI.Material.Tests;

public sealed class RecentTournamentsServiceTests : IDisposable
{
    private readonly InMemoryUiSettingsStorage _storage = new();
    private readonly string _tempRoot;
    private readonly List<string> _createdFiles = new();

    public RecentTournamentsServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "recent-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    private string MakeRealFile(string name = null)
    {
        var path = Path.Combine(_tempRoot, (name ?? Guid.NewGuid().ToString("N")) + ".wrt");
        File.WriteAllText(path, "{}");
        _createdFiles.Add(path);
        return path;
    }

    [Fact]
    public void Add_dedupes_case_insensitively_and_moves_to_front()
    {
        var svc = new RecentTournamentsService(_storage);
        var fileA = MakeRealFile("a");
        var fileB = MakeRealFile("b");

        svc.Add(fileA);
        svc.Add(fileB);
        svc.Add(fileA.ToUpperInvariant()); // re-add A with different casing

        _storage.Saved.RecentTournamentFiles.Should().HaveCount(2);
        _storage.Saved.RecentTournamentFiles[0].Should().BeEquivalentTo(fileA.ToUpperInvariant(),
            "newest add wins, even when only the casing differs from a prior entry");
        _storage.Saved.RecentTournamentFiles[1].Should().Be(fileB);
    }

    [Fact]
    public void Add_caps_list_at_max_items()
    {
        var svc = new RecentTournamentsService(_storage);
        for (int i = 0; i < RecentTournamentsService.MaxItems + 3; i++)
        {
            svc.Add(MakeRealFile("f" + i));
        }

        _storage.Saved.RecentTournamentFiles.Should().HaveCount(RecentTournamentsService.MaxItems);
    }

    [Fact]
    public void LoadExisting_prunes_entries_whose_files_no_longer_exist()
    {
        var svc = new RecentTournamentsService(_storage);
        var alive = MakeRealFile("alive");
        var dead = Path.Combine(_tempRoot, "missing.wrt"); // never created

        // Seed storage directly (bypassing Add's existence-blind contract — Add
        // is allowed to enqueue any path; it's LoadExisting that prunes).
        _storage.Saved = new LocalUiSettings
        {
            RecentTournamentFiles = new List<string> { dead, alive }
        };

        var surviving = svc.LoadExisting();

        surviving.Should().ContainSingle().Which.Should().Be(alive);
        _storage.Saved.RecentTournamentFiles.Should().NotContain(dead,
            "pruning result must be persisted so dead entries don't reappear next session");
    }

    [Fact]
    public void LoadExisting_does_not_persist_when_nothing_changed()
    {
        var svc = new RecentTournamentsService(_storage);
        var alive = MakeRealFile("steady");
        _storage.Saved = new LocalUiSettings
        {
            RecentTournamentFiles = new List<string> { alive }
        };
        _storage.SaveCount = 0;

        svc.LoadExisting();

        _storage.SaveCount.Should().Be(0,
            "no-op loads must not write back — Home reloads on every navigation");
    }

    private sealed class InMemoryUiSettingsStorage : ILocalUiSettingsStorage
    {
        public LocalUiSettings Saved { get; set; } = new LocalUiSettings();
        public int SaveCount { get; set; }

        public LocalUiSettings Load()
        {
            return new LocalUiSettings
            {
                BaseTheme = Saved.BaseTheme,
                PrimaryColor = Saved.PrimaryColor,
                SecondaryColor = Saved.SecondaryColor,
                LanguageCode = Saved.LanguageCode,
                RecentTournamentFiles = Saved.RecentTournamentFiles == null
                    ? new List<string>()
                    : Saved.RecentTournamentFiles.ToList(),
            };
        }

        public void Save(LocalUiSettings settings)
        {
            Saved = settings;
            SaveCount++;
        }
    }
}
