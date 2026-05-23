using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Wrestling.Data;
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

    // Load compat: pre-rename .wrt files (when mats were called "carpets") used
    // "Carpets", "CarpetID", "CarpetLabel" property names. LegacyMatNameConverter
    // remaps them to the new names so old tournaments still open.
    [Fact]
    public void ReadFromFile_maps_legacy_carpet_keys_to_mat_properties()
    {
        var path = Path_("legacy.wrt");
        var matId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        File.WriteAllText(path, $@"{{
  ""Name"": ""Old"",
  ""Carpets"": [{{ ""ID"": ""{matId}"", ""Name"": ""Mat 1"", ""Groups"": [] }}],
  ""Groups"": [{{ ""ID"": ""{groupId}"", ""CarpetID"": ""{matId}"", ""CarpetLabel"": ""Mat 1"", ""Wrestlers"": [] }}]
}}");

        var info = _storage.ReadFromFile<TournamentInfo>(path);

        info.Should().NotBeNull();
        info.Mats.Should().HaveCount(1);
        info.Mats.Single().ID.Should().Be(matId);
        info.Groups.Should().HaveCount(1);
        info.Groups.Single().MatID.Should().Be(matId);
        info.Groups.Single().MatLabel.Should().Be("Mat 1");
    }

    [Fact]
    public async Task ReadFromFileAsync_maps_legacy_carpet_keys_to_mat_properties()
    {
        var path = Path_("legacy-async.wrt");
        var matId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        File.WriteAllText(path, $@"{{
  ""Carpets"": [{{ ""ID"": ""{matId}"", ""Name"": ""A"", ""Groups"": [] }}],
  ""Groups"": [{{ ""ID"": ""{groupId}"", ""CarpetID"": ""{matId}"", ""CarpetLabel"": ""A"", ""Wrestlers"": [] }}]
}}");

        var info = await _storage.ReadFromFileAsync<TournamentInfo>(path);

        info.Should().NotBeNull();
        info.Mats.Single().ID.Should().Be(matId);
        info.Groups.Single().MatID.Should().Be(matId);
    }

    // Save path always emits the new names. After a save+reload cycle the
    // legacy keys must not reappear.
    [Fact]
    public void Save_then_Load_uses_new_mat_keys_only()
    {
        var path = Path_("roundtrip.wrt");
        var matId = Guid.NewGuid();
        var info = new TournamentInfo
        {
            Mats = new[] { new MatInfo { ID = matId, Name = "Mat" } },
            Groups = new[] { new AgeWeightGroupInfo { ID = Guid.NewGuid(), MatID = matId, MatLabel = "Mat" } },
        };

        _storage.SaveToFile(info, path).Should().BeTrue();
        var json = File.ReadAllText(path);

        json.Should().Contain("\"Mats\"").And.Contain("\"MatID\"").And.Contain("\"MatLabel\"");
        json.Should().NotContain("\"Carpets\"").And.NotContain("\"CarpetID\"").And.NotContain("\"CarpetLabel\"");
    }

    // Hardening: TypeNameHandling.None blocks $type-based polymorphic
    // deserialization gadgets. A .wrt forged with a $type pointing to e.g.
    // ObjectDataProvider must NOT instantiate that type — instead the parser
    // either drops the property or returns null on a property type mismatch.
    [Fact]
    public void ReadFromFile_ignores_dollar_type_polymorphism_marker()
    {
        var path = Path_("typename.json");
        File.WriteAllText(path, @"{
  ""$type"": ""System.Windows.Data.ObjectDataProvider, PresentationFramework"",
  ""Name"": ""benign""
}");

        Payload result = null;
        Action act = () => result = _storage.ReadFromFile<Payload>(path);

        act.Should().NotThrow("malformed $type must not blow up the load");
        // Either Payload deserialized with Name (if Newtonsoft skipped $type)
        // or null was returned — both are safe outcomes. The unsafe outcome
        // (instantiating ObjectDataProvider via $type) is the one we forbid.
        if (result != null)
        {
            result.Name.Should().Be("benign");
        }
    }

    // Orphan-tmp cleanup (#5): stale *.tmp.<guid> residue from a previous
    // crashed save is reaped when the load reads any file from the same dir.
    [Fact]
    public void ReadFromFile_reaps_stale_orphan_tmp_files_in_same_directory()
    {
        var realPath = Path_("real.json");
        _storage.SaveToFile(new Payload { Name = "ok" }, realPath).Should().BeTrue();

        var staleOrphan = Path_("real.json.tmp.deadbeef000111222333444555666777");
        File.WriteAllText(staleOrphan, "{}");
        File.SetLastWriteTimeUtc(staleOrphan, DateTime.UtcNow.AddHours(-1));

        _storage.ReadFromFile<Payload>(realPath).Should().NotBeNull();

        File.Exists(staleOrphan).Should().BeFalse(
            "an hour-old *.tmp.* sibling is residue from a crashed atomic write");
    }

    // Don't reap *.tmp.* siblings that are still fresh — they might belong
    // to a peer mat's in-flight write (or to ours, in a parallel save).
    [Fact]
    public void ReadFromFile_keeps_fresh_tmp_files_intact()
    {
        var realPath = Path_("real-fresh.json");
        _storage.SaveToFile(new Payload { Name = "ok" }, realPath).Should().BeTrue();

        var freshTmp = Path_("real-fresh.json.tmp.aaaabbbbccccdddd1111222233334444");
        File.WriteAllText(freshTmp, "{}");
        // mtime defaults to "now" — within the 5-min protection window

        _storage.ReadFromFile<Payload>(realPath).Should().NotBeNull();

        File.Exists(freshTmp).Should().BeTrue(
            "fresh tmp residue must not be deleted — a peer/parallel save may still be flushing it");
    }
}
