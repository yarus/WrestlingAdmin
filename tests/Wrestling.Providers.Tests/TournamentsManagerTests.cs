using System;
using System.Threading.Tasks;
using FluentAssertions;
using Wrestling.Data;
using Wrestling.DataAccess;
using Wrestling.Entities;
using Wrestling.Providers;
using Xunit;

namespace Wrestling.Providers.Tests;

public class TournamentsManagerTests
{
    private sealed class FakeTournamentDataAccess : ITournamentDataAccess
    {
        public TournamentInfo Stored { get; set; }
        public string LastSavedFileName { get; private set; }
        public int SaveCount { get; private set; }

        public TournamentInfo LoadFromFile(string fileName) => Stored;
        public Task<TournamentInfo> LoadFromFileAsync(string fileName) => Task.FromResult(Stored);

        public bool SaveToFile(TournamentInfo item, string fileName)
        {
            Stored = item;
            LastSavedFileName = fileName;
            SaveCount++;
            return true;
        }

        public Task<bool> SaveToFileAsync(TournamentInfo item, string fileName)
        {
            SaveToFile(item, fileName);
            return Task.FromResult(true);
        }
    }

    [Fact]
    public void SaveToFile_sets_FileName_on_entity_when_save_succeeds()
    {
        var da = new FakeTournamentDataAccess();
        var mgr = new TournamentsManager(da, new EntityToInfoAdapter());
        var t = new Tournament(new GlobalSettings()) { ID = Guid.NewGuid(), Name = "X" };

        mgr.SaveToFile(t, "foo.wrt").Should().BeTrue();
        t.FileName.Should().Be("foo.wrt");
        da.LastSavedFileName.Should().Be("foo.wrt");
    }

    [Fact]
    public async Task SaveToFileAsync_sets_FileName_on_entity_when_save_succeeds()
    {
        var da = new FakeTournamentDataAccess();
        var mgr = new TournamentsManager(da, new EntityToInfoAdapter());
        var t = new Tournament(new GlobalSettings()) { ID = Guid.NewGuid(), Name = "X" };

        (await mgr.SaveToFileAsync(t, "bar.wrt")).Should().BeTrue();
        t.FileName.Should().Be("bar.wrt");
    }

    [Fact]
    public void LoadFromFile_returns_null_when_backend_returns_null()
    {
        var da = new FakeTournamentDataAccess { Stored = null };
        var mgr = new TournamentsManager(da, new EntityToInfoAdapter());
        mgr.LoadFromFile("missing.wrt").Should().BeNull();
    }

    [Fact]
    public async Task LoadFromFileAsync_populates_FileName_from_given_path()
    {
        var da = new FakeTournamentDataAccess
        {
            Stored = new TournamentInfo
            {
                ID = Guid.NewGuid(),
                Name = "Persisted",
                Status = TournamentStatus.Pending.ToString(),
                Settings = new GlobalSettingsInfo()
            }
        };
        var mgr = new TournamentsManager(da, new EntityToInfoAdapter());

        var t = await mgr.LoadFromFileAsync("persisted.wrt");

        t.Should().NotBeNull();
        t.Name.Should().Be("Persisted");
        t.FileName.Should().Be("persisted.wrt");
    }
}
