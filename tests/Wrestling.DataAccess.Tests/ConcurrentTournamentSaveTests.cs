using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Wrestling.Data;
using Wrestling.DataAccess;
using Xunit;

namespace Wrestling.DataAccess.Tests;

public sealed class ConcurrentTournamentSaveTests
{
    [Fact]
    public async Task Delayed_old_save_cannot_overwrite_three_newer_snapshots()
    {
        var dir = Path.Combine(Path.GetTempPath(), "wrestling-race-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "admin.wrt");
        var storage = new DelayedStorage();
        var first = new TournamentDataAccess(storage);
        var second = new TournamentDataAccess(storage);
        try
        {
            var old = first.SaveToFileAsync(Info("manual-before-merges"), path);
            await storage.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var mat1 = second.SaveToFileAsync(Info("mat1"), Path.Combine(dir, ".", "admin.wrt"));
            var mat2 = first.SaveToFileAsync(Info("mat1+mat2"), path.ToUpperInvariant());
            var mat3 = second.SaveToFileAsync(Info("mat1+mat2+mat3"), path);
            var writesBeforeRelease = storage.Writes;
            storage.Release.TrySetResult();
            (await Task.WhenAll(old, mat1, mat2, mat3)).Should().OnlyContain(ok => ok);
            first.LoadFromFile(path).Name.Should().Be("mat1+mat2+mat3");
            writesBeforeRelease.Should().Be(1, "all writes to the same file must wait for the first save");
            Directory.GetFiles(dir, "*.tmp.*").Should().BeEmpty();
            Directory.GetFiles(Path.Combine(dir, "Backups", "admin"), "*.wrt").Should().HaveCount(3);
        }
        finally
        {
            storage.Release.TrySetResult();
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task Cancelled_waiter_does_not_block_the_next_save()
    {
        var dir = Path.Combine(Path.GetTempPath(), "wrestling-race-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "admin.wrt");
        var storage = new DelayedStorage();
        var access = new TournamentDataAccess(storage);
        try
        {
            var old = access.SaveToFileAsync(Info("old"), path);
            await storage.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            using var cancellation = new CancellationTokenSource();
            var cancelled = access.SaveToFileAsync(Info("cancelled"), path, cancellation.Token);
            cancellation.Cancel();
            storage.Release.TrySetResult();
            await old;
            Func<Task> wait = async () => await cancelled;
            await wait.Should().ThrowAsync<OperationCanceledException>();
            (await access.SaveToFileAsync(Info("latest"), path)).Should().BeTrue();
            access.LoadFromFile(path).Name.Should().Be("latest");
        }
        finally { storage.Release.TrySetResult(); Directory.Delete(dir, true); }
    }

    private static TournamentInfo Info(string name) => new()
    {
        Name = name, Settings = new GlobalSettingsInfo()
    };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Failed_write_releases_the_gate_and_does_not_report_success(bool throws)
    {
        var dir = Path.Combine(Path.GetTempPath(), "wrestling-race-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "admin.wrt");
        var storage = new DelayedStorage { FailFirstWrite = true, ThrowOnFailure = throws };
        var access = new TournamentDataAccess(storage);
        try
        {
            var failed = access.SaveToFileAsync(Info("failed"), path);
            await storage.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var next = access.SaveToFileAsync(Info("latest"), path);
            storage.Release.TrySetResult();
            if (throws)
            {
                Func<Task> wait = async () => await failed;
                await wait.Should().ThrowAsync<IOException>();
            }
            else (await failed).Should().BeFalse();
            (await next.WaitAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
            access.LoadFromFile(path).Name.Should().Be("latest");
        }
        finally { storage.Release.TrySetResult(); Directory.Delete(dir, true); }
    }

    private sealed class DelayedStorage : IStorageDataAccess
    {
        private readonly JsonStorageDataAccess _inner = new();
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Writes;
        public bool FailFirstWrite;
        public bool ThrowOnFailure;
        public async Task<bool> SaveToFileAsync<T>(T item, string fileName, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref Writes) == 1)
            {
                Entered.TrySetResult();
                await Release.Task.WaitAsync(cancellationToken);
                if (FailFirstWrite)
                {
                    if (ThrowOnFailure) throw new IOException("Injected write failure");
                    return false;
                }
            }
            return await _inner.SaveToFileAsync(item, fileName, cancellationToken);
        }
        public bool SaveToFile<T>(T item, string path) => _inner.SaveToFile(item, path);
        public T ReadFromFile<T>(string path) => _inner.ReadFromFile<T>(path);
        public Task<T> ReadFromFileAsync<T>(string path, CancellationToken ct = default) => _inner.ReadFromFileAsync<T>(path, ct);
        public void ProcessDirectory<T>(string path, ref List<T> list, string mask) => _inner.ProcessDirectory(path, ref list, mask);
        public void SaveToStorage<T>(T item, string folder, string name) => _inner.SaveToStorage(item, folder, name);
        public IEnumerable<string> GetFileNamesInDirectory(string path, string mask) => _inner.GetFileNamesInDirectory(path, mask);
    }
}
