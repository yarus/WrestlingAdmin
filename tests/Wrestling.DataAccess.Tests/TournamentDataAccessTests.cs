using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Wrestling.Data;
using Wrestling.DataAccess;
using Xunit;

namespace Wrestling.DataAccess.Tests;

public sealed class TournamentDataAccessTests : IDisposable
{
    private readonly string _dir;
    private readonly TournamentDataAccess _da;

    public TournamentDataAccessTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "wrestling-td-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _da = new TournamentDataAccess(new JsonStorageDataAccess());
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    [Fact]
    public void SaveToFile_and_LoadFromFile_round_trip_TournamentInfo()
    {
        var path = Path.Combine(_dir, "t.wrt");
        var info = new TournamentInfo
        {
            ID = Guid.NewGuid(),
            Name = "Round-trip",
            StartDate = new DateTime(2026, 4, 19),
            Status = "InProgress",
            Settings = new GlobalSettingsInfo { MaxRoundSecond = 120 }
        };

        _da.SaveToFile(info, path).Should().BeTrue();
        var restored = _da.LoadFromFile(path);

        restored.Should().NotBeNull();
        restored.ID.Should().Be(info.ID);
        restored.Name.Should().Be("Round-trip");
        restored.Status.Should().Be("InProgress");
        restored.Settings.MaxRoundSecond.Should().Be(120);
    }

    [Fact]
    public async Task LoadFromFileAsync_round_trips()
    {
        var path = Path.Combine(_dir, "async.wrt");
        var info = new TournamentInfo { ID = Guid.NewGuid(), Name = "A" };

        (await _da.SaveToFileAsync(info, path)).Should().BeTrue();
        var restored = await _da.LoadFromFileAsync(path);

        restored.Should().NotBeNull();
        restored.Name.Should().Be("A");
    }
}
