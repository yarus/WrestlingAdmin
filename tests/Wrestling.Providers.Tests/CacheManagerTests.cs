using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Wrestling.Data;
using Wrestling.DataAccess;
using Wrestling.Entities;
using Wrestling.Providers;
using Xunit;

namespace Wrestling.Providers.Tests;

public class CacheManagerTests : IDisposable
{
    private readonly string _dir;

    public CacheManagerTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "wrestling-cache-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    private sealed class FakeTeamsDataAccess : ITeamsDataAccess
    {
        public Dictionary<string, List<TeamApplicationInfo>> Saved { get; } = new();
        public Dictionary<string, List<TeamApplicationInfo>> Preload { get; } = new();

        public bool SaveToFile(List<TeamApplicationInfo> list, string fileName)
        {
            Saved[fileName] = list;
            return true;
        }

        public List<TeamApplicationInfo> LoadFromFile(string fileName)
        {
            return Preload.TryGetValue(fileName, out var list) ? list : null;
        }
    }

    private sealed class FakeWrestlersDataAccess : IWrestlersDataAccess
    {
        public Dictionary<string, List<WrestlerInfo>> Saved { get; } = new();
        public Dictionary<string, List<WrestlerInfo>> Preload { get; } = new();

        public bool SaveToFile(List<WrestlerInfo> list, string fileName)
        {
            Saved[fileName] = list;
            return true;
        }

        public List<WrestlerInfo> LoadFromFile(string fileName)
        {
            return Preload.TryGetValue(fileName, out var list) ? list : null;
        }
    }

    private (CacheManager cache, FakeTeamsDataAccess teams, FakeWrestlersDataAccess wrestlers) Setup()
    {
        var teams = new FakeTeamsDataAccess();
        var wrestlers = new FakeWrestlersDataAccess();
        var adapter = new EntityToInfoAdapter();
        return (new CacheManager(teams, wrestlers, adapter, _dir), teams, wrestlers);
    }

    [Fact]
    public void SaveTeams_writes_into_configured_cache_directory()
    {
        var (cache, teams, _) = Setup();

        var team = new TeamApplication { ID = Guid.NewGuid(), ShortName = "BB", City = "Москва" };
        cache.SaveTeams(new List<TeamApplication> { team }).Should().BeTrue();

        var expectedPath = Path.Combine(_dir, "Cache_Teams.json");
        teams.Saved.Should().ContainKey(expectedPath);
        teams.Saved[expectedPath][0].ShortName.Should().Be("BB");
    }

    [Fact]
    public void LoadTeams_returns_empty_list_when_cache_missing()
    {
        var (cache, _, _) = Setup();
        cache.LoadTeams().Should().BeEmpty();
    }

    [Fact]
    public void LoadWrestlers_returns_mapped_entities_from_stored_infos()
    {
        var (cache, _, wrestlers) = Setup();

        var wId = Guid.NewGuid();
        var key = Path.Combine(_dir, "Cache_Wrestlers.json");
        wrestlers.Preload[key] = new List<WrestlerInfo>
        {
            new() { ID = wId, FirstName = "Иван", LastName = "Иванов", BirthDate = new DateTime(2005, 1, 1), Weight = 60 }
        };

        var result = cache.LoadWrestlers();
        result.Should().HaveCount(1);
        result[0].ID.Should().Be(wId);
        result[0].FullName.Should().Be("Иванов Иван");
    }

    [Fact]
    public void Default_constructor_points_cache_under_LocalAppData()
    {
        var teams = new FakeTeamsDataAccess();
        var wrestlers = new FakeWrestlersDataAccess();
        var cache = new CacheManager(teams, wrestlers, new EntityToInfoAdapter());

        cache.SaveTeams(new List<TeamApplication>()).Should().BeTrue();
        var expectedRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WrestlingAdmin");
        teams.Saved.Keys.Should().ContainSingle(k => k.StartsWith(expectedRoot));
    }
}
