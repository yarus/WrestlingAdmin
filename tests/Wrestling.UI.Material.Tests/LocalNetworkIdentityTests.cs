using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using Wrestling.DataAccess;
using Wrestling.UI.Material.Model;
using Xunit;

namespace Wrestling.UI.Material.Tests;

public sealed class LocalNetworkIdentityTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "WrestlingIdentity-" + Guid.NewGuid().ToString("N"));
    private string FileName => Path.Combine(_directory, "network_identity.json");

    [Fact]
    public void Name_defaults_to_windows_then_persists_without_ports_or_addresses()
    {
        var storage = new JsonStorageDataAccess();
        var identity = new LocalNetworkIdentity(storage, FileName);
        identity.NodeName.Should().Be(Environment.MachineName);
        identity.NodeName = "  Ковёр B  ";
        identity.NodeName.Should().Be("Ковёр B");
        identity.SaveError.Should().BeNull();
        new LocalNetworkIdentity(storage, FileName).NodeName.Should().Be("Ковёр B");
        var saved = JObject.Parse(File.ReadAllText(FileName));
        saved.Properties().Should().ContainSingle().Which.Name.Should().Be("NodeName");
        identity.NodeName = " ";
        new LocalNetworkIdentity(storage, FileName).NodeName.Should().Be(Environment.MachineName);
    }

    [Fact]
    public void A_different_computers_profile_does_not_reuse_the_other_local_name()
    {
        var storage = new JsonStorageDataAccess();
        new LocalNetworkIdentity(storage, FileName).NodeName = "Ковёр A";
        var otherFile = Path.Combine(_directory, "other-computer", "network_identity.json");
        new LocalNetworkIdentity(storage, otherFile).NodeName.Should().Be(Environment.MachineName);
        File.Exists(otherFile).Should().BeFalse();
    }

    [Fact]
    public void Corrupt_local_profile_falls_back_to_windows_name_without_throwing()
    {
        Directory.CreateDirectory(_directory); File.WriteAllText(FileName, "broken json");
        new LocalNetworkIdentity(new JsonStorageDataAccess(), FileName).NodeName.Should().Be(Environment.MachineName);
    }

    [Fact]
    public void Failed_name_write_keeps_session_name_and_exposes_persistent_warning()
    {
        Directory.CreateDirectory(_directory);
        var blocker = Path.Combine(_directory, "not-a-directory"); File.WriteAllText(blocker, "blocked");
        var identity = new LocalNetworkIdentity(new JsonStorageDataAccess(), Path.Combine(blocker, "identity.json"));
        identity.NodeName = "Session only";
        identity.NodeName.Should().Be("Session only");
        identity.SaveError.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void A_false_save_result_is_reported_instead_of_claiming_persistence()
    {
        var identity = new LocalNetworkIdentity(new DecliningStorage(), FileName);
        identity.NodeName = "Session only";
        identity.SaveError.Should().NotBeNullOrEmpty();
        File.Exists(FileName).Should().BeFalse();
    }

    private sealed class DecliningStorage : IStorageDataAccess
    {
        public T ReadFromFile<T>(string path) => default;
        public Task<T> ReadFromFileAsync<T>(string path, CancellationToken cancellationToken = default) => Task.FromResult(default(T));
        public bool SaveToFile<T>(T item, string fileName) => false;
        public Task<bool> SaveToFileAsync<T>(T item, string fileName, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public void ProcessDirectory<T>(string targetDirectory, ref List<T> list, string mask) => throw new NotSupportedException();
        public void SaveToStorage<T>(T item, string storageFolder, string fileName) => throw new NotSupportedException();
        public IEnumerable<string> GetFileNamesInDirectory(string path, string mask) => throw new NotSupportedException();
    }

    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
}
