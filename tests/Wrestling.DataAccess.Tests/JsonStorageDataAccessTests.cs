using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Wrestling.DataAccess;
using Xunit;

namespace Wrestling.DataAccess.Tests;

public sealed class JsonStorageDataAccessTests : IDisposable
{
    private readonly string _dir;
    private readonly JsonStorageDataAccess _storage = new();

    public JsonStorageDataAccessTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "wrestling-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    private string Path_(string name) => Path.Combine(_dir, name);

    public sealed class Payload
    {
        public string Name { get; set; }
        public int Age { get; set; }
        public DateTime When { get; set; }
    }

    [Fact]
    public void SaveToFile_then_ReadFromFile_round_trips_object()
    {
        var path = Path_("p.json");
        var input = new Payload { Name = "Иван", Age = 42, When = new DateTime(2026, 4, 19) };

        _storage.SaveToFile(input, path).Should().BeTrue();
        File.Exists(path).Should().BeTrue();

        var restored = _storage.ReadFromFile<Payload>(path);
        restored.Should().NotBeNull();
        restored.Name.Should().Be("Иван");
        restored.Age.Should().Be(42);
        restored.When.Should().Be(new DateTime(2026, 4, 19));
    }

    [Fact]
    public async Task SaveToFileAsync_then_ReadFromFileAsync_round_trips()
    {
        var path = Path_("p2.json");
        var input = new Payload { Name = "N", Age = 1, When = DateTime.Today };

        (await _storage.SaveToFileAsync(input, path)).Should().BeTrue();
        var restored = await _storage.ReadFromFileAsync<Payload>(path);

        restored.Should().NotBeNull();
        restored.Name.Should().Be("N");
    }

    [Fact]
    public void ReadFromFile_returns_default_when_file_is_missing()
    {
        var result = _storage.ReadFromFile<Payload>(Path_("nope.json"));
        result.Should().BeNull();
    }

    [Fact]
    public async Task ReadFromFileAsync_returns_default_when_file_is_missing()
    {
        var result = await _storage.ReadFromFileAsync<Payload>(Path_("none.json"));
        result.Should().BeNull();
    }

    public sealed class ThrowsOnSerialize
    {
        public string Good { get; set; }
        public string Evil
        {
            get => throw new InvalidOperationException("serialize boom");
            set { }
        }
    }

    // Bug-driver: SaveToFile currently swallows exceptions and returns false.
    // Desired behavior per CLAUDE.md ("prefer throwing over swallowing") is to
    // surface the error so the app-level crash handlers can back up the data.
    [Fact]
    public void SaveToFile_surfaces_serialization_failure_instead_of_swallowing()
    {
        var path = Path_("evil.json");
        Action act = () => _storage.SaveToFile(new ThrowsOnSerialize { Good = "ok" }, path);

        act.Should().Throw<Exception>(
            "serialization errors must not be silently hidden behind a boolean result");
    }

    // Bug-driver: without atomic writes, a failure mid-serialize truncates
    // the existing file and loses prior-good content.
    [Fact]
    public void SaveToFile_preserves_existing_file_when_new_write_fails()
    {
        var path = Path_("atomic.json");
        var original = new Payload { Name = "Оригинал", Age = 7, When = DateTime.Today };

        _storage.SaveToFile(original, path).Should().BeTrue();
        var originalContent = File.ReadAllText(path);

        try { _storage.SaveToFile(new ThrowsOnSerialize { Good = "fail" }, path); }
        catch { /* expected once save is fixed to surface errors */ }

        File.Exists(path).Should().BeTrue("failed write must never delete the prior save");
        File.ReadAllText(path).Should().Be(originalContent,
            "atomic-write contract: partial writes go to a temp file, never the live one");
    }

    // Load contract: import polls flaky network paths on a timer during live
    // matches, so Read paths must NEVER throw for expected I/O / parse errors.
    [Fact]
    public void ReadFromFile_returns_default_when_file_is_corrupt_and_does_not_throw()
    {
        var path = Path_("corrupt.json");
        File.WriteAllText(path, "{\"Name\":\"partial...");

        Payload result = default;
        Action act = () => result = _storage.ReadFromFile<Payload>(path);

        act.Should().NotThrow("load must tolerate malformed JSON without crashing the app");
        result.Should().BeNull("corrupt content yields default(T), not a partial object");
    }

    [Fact]
    public async Task ReadFromFileAsync_returns_default_when_file_is_corrupt_and_does_not_throw()
    {
        var path = Path_("corrupt-async.json");
        File.WriteAllText(path, "]}{ not json");

        Payload result = default;
        Func<Task> act = async () => result = await _storage.ReadFromFileAsync<Payload>(path);

        await act.Should().NotThrowAsync("async load must tolerate malformed JSON");
        result.Should().BeNull();
    }

    [Fact]
    public void ReadFromFile_returns_default_when_path_is_invalid_and_does_not_throw()
    {
        Payload result = default;
        Action act = () => result = _storage.ReadFromFile<Payload>("\0:/\\invalid");

        act.Should().NotThrow("invalid paths (e.g. from stale ImportSources) must not crash");
        result.Should().BeNull();
    }
}
