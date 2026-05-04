using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Wrestling.Data;
using Wrestling.DataAccess;
using Xunit;

namespace Wrestling.DataAccess.Tests;

// Backup-before-save protection for tournament files. The atomic write in
// JsonStorageDataAccess already blocks torn writes, but a semantic bug could
// still overwrite a good tournament with corrupt content. Rotated backups
// give a known-good fallback.
//
// Policy comes from GlobalSettingsInfo on the saved tournament:
//   - IsBackupEnabled master toggle
//   - MaxBackupCount retention (default 20)
//   - BackupFolderPath override (empty => <tournament-dir>/Backups)
// Backups are further namespaced per tournament file inside that root, so
// multiple tournaments sharing a working directory don't mix.
public sealed class TournamentBackupTests : IDisposable
{
    private readonly string _dir;
    private readonly TournamentDataAccess _da;

    public TournamentBackupTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "wrestling-backup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _da = new TournamentDataAccess(new JsonStorageDataAccess());
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    private string TournamentPath(string name = "t.wrt") => Path.Combine(_dir, name);
    private string DefaultBackupFolder(string name = "t.wrt") =>
        Path.Combine(_dir, "Backups", Path.GetFileNameWithoutExtension(name));

    private static TournamentInfo NewInfo(string marker, GlobalSettingsInfo settings = null) => new()
    {
        ID = Guid.NewGuid(),
        Name = marker,
        StartDate = new DateTime(2026, 4, 19),
        Status = "InProgress",
        Settings = settings ?? new GlobalSettingsInfo()
    };

    [Fact]
    public void First_save_creates_no_backup_folder()
    {
        var path = TournamentPath();

        _da.SaveToFile(NewInfo("v1"), path);

        Directory.Exists(DefaultBackupFolder()).Should().BeFalse(
            "a backup is the pre-existing content; nothing existed before the first save");
    }

    [Fact]
    public void Second_save_creates_backup_at_default_location_containing_prior_content()
    {
        var path = TournamentPath();

        _da.SaveToFile(NewInfo("v1"), path);
        _da.SaveToFile(NewInfo("v2"), path);

        Directory.Exists(DefaultBackupFolder()).Should().BeTrue();
        var files = Directory.GetFiles(DefaultBackupFolder(), "*.wrt");
        files.Should().HaveCount(1);
        File.ReadAllText(files[0]).Should().Contain("\"Name\":\"v1\"",
            "the backup captures what existed before the v2 save");
    }

    [Fact]
    public void Multiple_tournaments_in_same_directory_do_not_mix_backups()
    {
        var pathA = TournamentPath("alpha.wrt");
        var pathB = TournamentPath("beta.wrt");

        _da.SaveToFile(NewInfo("a1"), pathA);
        _da.SaveToFile(NewInfo("a2"), pathA);
        _da.SaveToFile(NewInfo("b1"), pathB);
        _da.SaveToFile(NewInfo("b2"), pathB);

        var alphaBackups = Directory.GetFiles(DefaultBackupFolder("alpha.wrt"), "*.wrt");
        var betaBackups = Directory.GetFiles(DefaultBackupFolder("beta.wrt"), "*.wrt");

        alphaBackups.Should().HaveCount(1);
        betaBackups.Should().HaveCount(1);
        File.ReadAllText(alphaBackups[0]).Should().Contain("\"Name\":\"a1\"");
        File.ReadAllText(betaBackups[0]).Should().Contain("\"Name\":\"b1\"");
    }

    [Fact]
    public void Backup_rotation_respects_configured_MaxBackupCount()
    {
        var path = TournamentPath();
        var settings = new GlobalSettingsInfo { IsBackupEnabled = true, MaxBackupCount = 5 };

        _da.SaveToFile(NewInfo("v0", settings), path);
        for (int i = 1; i <= 10; i++)
        {
            System.Threading.Thread.Sleep(2); // distinct timestamps
            _da.SaveToFile(NewInfo($"v{i}", settings), path);
        }

        Directory.GetFiles(DefaultBackupFolder(), "*.wrt").Should().HaveCount(5);
    }

    [Fact]
    public void Default_retention_when_unset_is_twenty()
    {
        var path = TournamentPath();

        _da.SaveToFile(NewInfo("v0"), path);
        for (int i = 1; i <= 25; i++)
        {
            System.Threading.Thread.Sleep(2);
            _da.SaveToFile(NewInfo($"v{i}"), path);
        }

        Directory.GetFiles(DefaultBackupFolder(), "*.wrt").Should().HaveCount(20);
    }

    [Fact]
    public void Backup_rotation_drops_oldest_first()
    {
        var path = TournamentPath();
        var settings = new GlobalSettingsInfo { IsBackupEnabled = true, MaxBackupCount = 10 };

        _da.SaveToFile(NewInfo("v0", settings), path);
        for (int i = 1; i <= 12; i++)
        {
            _da.SaveToFile(NewInfo($"v{i}", settings), path);
            System.Threading.Thread.Sleep(2);
        }

        var backups = Directory.GetFiles(DefaultBackupFolder(), "*.wrt")
            .OrderBy(p => p)
            .ToList();

        backups.Should().HaveCount(10);
        var contents = backups.Select(File.ReadAllText).ToList();
        contents.Should().NotContain(c => c.Contains("\"Name\":\"v0\""),
            "the oldest backup must be rotated out first");
        contents.Should().Contain(c => c.Contains("\"Name\":\"v11\""),
            "the newest backup must be retained");
    }

    [Fact]
    public void Backup_disabled_flag_suppresses_all_backup_work()
    {
        var path = TournamentPath();
        var settings = new GlobalSettingsInfo { IsBackupEnabled = false };

        _da.SaveToFile(NewInfo("v1", settings), path);
        _da.SaveToFile(NewInfo("v2", settings), path);
        _da.SaveToFile(NewInfo("v3", settings), path);

        Directory.Exists(DefaultBackupFolder()).Should().BeFalse(
            "no backup folder should be created when backups are disabled");
    }

    [Fact]
    public void Custom_relative_folder_path_resolves_against_tournament_directory()
    {
        var path = TournamentPath();
        var settings = new GlobalSettingsInfo
        {
            IsBackupEnabled = true,
            MaxBackupCount = 10,
            BackupFolderPath = "custom-backups"
        };

        _da.SaveToFile(NewInfo("v1", settings), path);
        _da.SaveToFile(NewInfo("v2", settings), path);

        var expected = Path.Combine(_dir, "custom-backups", "t");
        Directory.Exists(expected).Should().BeTrue();
        Directory.GetFiles(expected, "*.wrt").Should().HaveCount(1);
        Directory.Exists(DefaultBackupFolder()).Should().BeFalse(
            "the default Backups folder should not be used when a custom path is set");
    }

    [Fact]
    public void Custom_absolute_folder_path_is_honoured()
    {
        var path = TournamentPath();
        var absolute = Path.Combine(Path.GetTempPath(), "wrestling-abs-" + Guid.NewGuid().ToString("N"));
        try
        {
            var settings = new GlobalSettingsInfo
            {
                IsBackupEnabled = true,
                MaxBackupCount = 10,
                BackupFolderPath = absolute
            };

            _da.SaveToFile(NewInfo("v1", settings), path);
            _da.SaveToFile(NewInfo("v2", settings), path);

            var expected = Path.Combine(absolute, "t");
            Directory.Exists(expected).Should().BeTrue();
            Directory.GetFiles(expected, "*.wrt").Should().HaveCount(1);
        }
        finally
        {
            try { Directory.Delete(absolute, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task Async_save_also_creates_and_rotates_backups()
    {
        var path = TournamentPath();

        await _da.SaveToFileAsync(NewInfo("v1"), path);
        await _da.SaveToFileAsync(NewInfo("v2"), path);

        Directory.Exists(DefaultBackupFolder()).Should().BeTrue();
        Directory.GetFiles(DefaultBackupFolder(), "*.wrt").Should().HaveCount(1);
    }

    [Fact]
    public void Save_succeeds_even_when_backup_folder_cannot_be_created()
    {
        // Backups are best-effort — a filesystem that refuses to create the
        // backup folder (permissions, disk full, path collision) must not
        // block the save itself. The atomic write in the storage layer still
        // protects against torn files.
        var path = TournamentPath();
        _da.SaveToFile(NewInfo("v1"), path);

        // Block the default backup root by planting a file at its location
        // so Directory.CreateDirectory fails with an IOException.
        var blockerRoot = Path.Combine(_dir, "Backups");
        File.WriteAllText(blockerRoot, "not a folder");

        try
        {
            Action act = () => _da.SaveToFile(NewInfo("v2"), path);

            act.Should().NotThrow("a backup failure is never allowed to break the save");
            _da.LoadFromFile(path).Name.Should().Be("v2", "the save itself must have persisted");
        }
        finally
        {
            try { File.Delete(blockerRoot); } catch { }
        }
    }

    [Fact]
    public void Verification_failure_restores_latest_backup_and_returns_false()
    {
        var path = TournamentPath();
        _da.SaveToFile(NewInfo("good"), path);

        // Simulate post-save corruption by scribbling garbage over the file
        // after a successful write. We do this through a custom storage that
        // writes a non-JSON payload, which the verification step will reject.
        var corruptingStorage = new CorruptingStorage();
        var corruptingDa = new TournamentDataAccess(corruptingStorage);

        var result = corruptingDa.SaveToFile(NewInfo("corrupt"), path);

        result.Should().BeFalse("verification detected the corrupt write");
        var restored = _da.LoadFromFile(path);
        restored.Should().NotBeNull();
        restored.Name.Should().Be("good", "destination was restored from the latest backup");
    }

    // Storage that deliberately writes invalid JSON to simulate a serializer
    // bug producing a file that atomic-write preserves but that can't be
    // deserialized back. Only SaveToFile is exercised by the test.
    private sealed class CorruptingStorage : IStorageDataAccess
    {
        private readonly JsonStorageDataAccess _inner = new();
        public bool SaveToFile<T>(T item, string fileName)
        {
            File.WriteAllText(fileName, "{ this is: not json");
            return true;
        }
        public Task<bool> SaveToFileAsync<T>(T item, string fileName) => Task.FromResult(SaveToFile(item, fileName));
        public T ReadFromFile<T>(string path) => _inner.ReadFromFile<T>(path);
        public Task<T> ReadFromFileAsync<T>(string path) => _inner.ReadFromFileAsync<T>(path);
        public System.Collections.Generic.IEnumerable<string> GetFileNamesInDirectory(string path, string mask)
            => _inner.GetFileNamesInDirectory(path, mask);
        public void ProcessDirectory<T>(string targetDirectory, ref System.Collections.Generic.List<T> list, string mask)
            => _inner.ProcessDirectory(targetDirectory, ref list, mask);
        public void SaveToStorage<T>(T item, string storageFolder, string fileName) => _inner.SaveToStorage(item, storageFolder, fileName);
    }
}
