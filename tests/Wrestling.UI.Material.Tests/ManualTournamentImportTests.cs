using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Wrestling.DataAccess;
using Wrestling.Entities;
using Wrestling.Entities.Bracket;
using Wrestling.Providers;
using Wrestling.Providers.Network;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Material.Tests.Fakes;
using Wrestling.UI.Material.Tournament.Conducting;
using Xunit;
using WTournament = Wrestling.Entities.Tournament;

namespace Wrestling.UI.Material.Tests;

public sealed class ManualTournamentImportTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "WrestlingManualImportTests-" + Guid.NewGuid().ToString("N"));
    private readonly EntityToInfoAdapter _adapter = new();
    private readonly TournamentDataAccess _dataAccess = new(new JsonStorageDataAccess());
    private readonly TournamentsManager _manager;

    public ManualTournamentImportTests()
    {
        Directory.CreateDirectory(_folder);
        _manager = new TournamentsManager(_dataAccess, _adapter);
    }

    private WTournament MakeTournament()
    {
        var target = new WTournament(new GlobalSettings { IsBackupEnabled = false })
        {
            ID = Guid.NewGuid(), Name = "T", FileName = Path.Combine(_folder, "working.wrt")
        };
        var part = new TournamentPart { ID = Guid.NewGuid(), Name = "Часть 1" };
        target.Parts.Add(part);
        var group = new AgeWeightGroup { ID = Guid.NewGuid(), PartID = part.ID, BracketVersion = 1, FieldsVersion = 1 };
        target.Groups.Add(group);
        var mat = new Mat { ID = Guid.NewGuid(), Name = "A", ActivePartID = part.ID };
        target.Mats.Add(mat);
        mat.Groups.Add(group);
        group.MatID = mat.ID;
        for (var i = 0; i < 2; i++)
        {
            var wrestler = new Wrestler
            {
                ID = Guid.NewGuid(), GroupID = group.ID, LastName = "W" + i,
                IsEntryFeePaid = true, IsWeightApproved = true, SeedNumber = i + 1
            };
            group.Wrestlers.Add(wrestler);
            target.Wrestlers.Add(wrestler);
        }
        new RoundRobinGroupBracketProcessor().Generate(target, group);
        new MatMatchNumbersGenerator().Generate(target, new GroupBracketProcessorFactory());
        _manager.SaveToFile(target, target.FileName).Should().BeTrue();
        return target;
    }

    private WTournament Clone(WTournament target) => _adapter.GetEntityFromInfo(_adapter.GetInfoFromEntity(target));
    private TournamentImporter Importer(ITournamentsManager manager = null) =>
        new(manager ?? _manager, new GroupBracketProcessorFactory(), new MatMatchNumbersGenerator());
    private static WrestlingMatch Match(WTournament tournament) => tournament.Groups[0].Bracket.Rounds.SelectMany(r => r.RoundMatches).Single();

    private string SaveRemote(WTournament target, bool redWon = true)
    {
        var remote = Clone(target);
        var match = Match(remote);
        var processor = new RoundRobinGroupBracketProcessor();
        processor.Load(remote, remote.Groups[0]);
        match.PointsRed = redWon ? 2 : 0;
        match.PointsBlue = redWon ? 0 : 2;
        processor.CompleteMatch(match, redWon, MatchWinTypeEnum.PointsWin);
        match.Version = 1;
        remote.Groups[0].MaxRoundSecond += 10;
        remote.Groups[0].FieldsVersion++;
        var fileName = Path.Combine(_folder, "remote.wrt");
        _manager.SaveToFile(remote, fileName).Should().BeTrue();
        return fileName;
    }

    private ManualTournamentImportService Service(DataContext context, ITournamentImporter importer = null,
        ITournamentsManager manager = null, string backupRoot = null) =>
        new(context, importer ?? Importer(), manager ?? _manager, null, _adapter, _dataAccess,
            backupRoot ?? Path.Combine(_folder, "recovery"));

    [Fact]
    public async Task File_import_matches_the_automatic_merge_and_preserves_unsaved_data_in_recovery_copy()
    {
        var target = MakeTournament();
        var source = SaveRemote(target);
        var sourceContents = File.ReadAllText(source);
        target.Wrestlers[0].LastName = "Несохранённая правка";
        target.Wrestlers[0].Timestamp = DateTime.UtcNow.AddMinutes(1);
        var expected = Clone(target);
        var importer = Importer();
        var plan = await importer.PrepareAsync(expected, source);
        importer.Apply(expected, plan);
        var context = new DataContext { Tournament = target };
        var originalFileName = target.FileName;

        var result = await Service(context).ImportAsync(source);

        result.Status.Should().Be(ManualImportStatus.Completed);
        result.Import.Outcome.Should().Be(ImportOutcome.Imported);
        PeerStateHasher.Compute(target).Should().Be(PeerStateHasher.Compute(expected));
        target.FileName.Should().Be(originalFileName);
        var backup = _manager.LoadFromFile(result.BackupFileName);
        Match(backup).Status.Should().Be(MatchStatusEnum.Pending);
        backup.Wrestlers[0].LastName.Should().Be("Несохранённая правка");
        backup.Groups[0].MaxRoundSecond.Should().Be(target.Groups[0].MaxRoundSecond - 10);
        Match(_manager.LoadFromFile(originalFileName)).Status.Should().Be(MatchStatusEnum.Completed);
        File.ReadAllText(source).Should().Be(sourceContents);

        var repeated = await Service(context).ImportAsync(source);
        repeated.Import.Outcome.Should().Be(ImportOutcome.NoNewData);
        repeated.Import.ImportedCount.Should().Be(0);
        repeated.BackupFileName.Should().BeNull();
        Directory.GetFiles(Path.Combine(_folder, "recovery"), "*.wrt", SearchOption.AllDirectories).Should().ContainSingle();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Equal_or_older_file_does_not_attempt_backup_or_save(bool older)
    {
        var target = MakeTournament();
        var source = Path.Combine(_folder, "remote.wrt");
        _manager.SaveToFile(Clone(target), source).Should().BeTrue();
        if (older)
        {
            var processor = new RoundRobinGroupBracketProcessor();
            processor.Load(target, target.Groups[0]);
            processor.CompleteMatch(Match(target), true, MatchWinTypeEnum.PointsWin);
            Match(target).Version = 2;
        }
        var blockedRoot = Path.Combine(_folder, "not-a-directory");
        File.WriteAllText(blockedRoot, "backup creation must not be attempted");
        var manager = new FakeTournamentsManager();
        var before = PeerStateHasher.Compute(target);

        var result = await Service(new DataContext { Tournament = target }, manager: manager, backupRoot: blockedRoot).ImportAsync(source);

        result.Status.Should().Be(ManualImportStatus.Completed);
        result.Import.Outcome.Should().Be(ImportOutcome.NoNewData);
        result.BackupFileName.Should().BeNull();
        manager.SaveAsyncCount.Should().Be(0);
        PeerStateHasher.Compute(target).Should().Be(before);
    }

    [Fact]
    public async Task Conflict_without_accepted_changes_does_not_create_a_backup()
    {
        var target = MakeTournament();
        var source = SaveRemote(target, redWon: false);
        var processor = new RoundRobinGroupBracketProcessor();
        processor.Load(target, target.Groups[0]);
        Match(target).PointsRed = 2;
        processor.CompleteMatch(Match(target), true, MatchWinTypeEnum.PointsWin);
        Match(target).Version = 1;
        var before = PeerStateHasher.Compute(target);

        var result = await Service(new DataContext { Tournament = target }).ImportAsync(source);

        result.Import.Outcome.Should().Be(ImportOutcome.Conflict);
        result.Import.ImportedCount.Should().Be(0);
        result.BackupFileName.Should().BeNull();
        Directory.Exists(Path.Combine(_folder, "recovery")).Should().BeFalse();
        PeerStateHasher.Compute(target).Should().Be(before);
    }

    [Fact]
    public async Task Changes_accepted_by_auto_sync_during_backup_do_not_leave_an_unneeded_copy()
    {
        var target = MakeTournament();
        var source = SaveRemote(target);
        var backupAccess = new WaitingBackupAccess(_dataAccess);
        var manager = new FakeTournamentsManager();
        var service = new ManualTournamentImportService(new DataContext { Tournament = target }, Importer(),
            manager, null, _adapter, backupAccess, Path.Combine(_folder, "recovery"));
        var operation = service.ImportAsync(source);
        await backupAccess.Written.Task;
        var auto = Importer();
        auto.Apply(target, await auto.PrepareAsync(target, source));
        backupAccess.Release.TrySetResult(true);

        var result = await operation;

        result.Import.Outcome.Should().Be(ImportOutcome.NoNewData);
        result.BackupFileName.Should().BeNull();
        Directory.GetFiles(Path.Combine(_folder, "recovery"), "*.wrt", SearchOption.AllDirectories).Should().BeEmpty();
        manager.SaveAsyncCount.Should().Be(0);
    }

    [Fact]
    public async Task Preview_does_not_hide_exclusion_repairs_by_normalizing_the_live_snapshot()
    {
        var target = MakeTournament();
        var source = Path.Combine(_folder, "remote.wrt");
        _manager.SaveToFile(Clone(target), source).Should().BeTrue();
        target.Wrestlers[0].IsNoShow = true;

        var result = await Service(new DataContext { Tournament = target }).ImportAsync(source);

        result.Import.ImportedCount.Should().BeGreaterThan(0);
        result.BackupFileName.Should().NotBeNull();
        _dataAccess.LoadFromFile(result.BackupFileName).Wrestlers.First().IsNoShow.Should().BeTrue();
        target.Wrestlers[0].IsNoShow.Should().BeFalse();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Missing_corrupt_or_foreign_files_do_not_apply_or_create_a_backup(bool foreign)
    {
        var target = MakeTournament();
        var original = File.ReadAllText(target.FileName);
        var source = Path.Combine(_folder, "bad.wrt");
        if (foreign) _manager.SaveToFile(new WTournament(new GlobalSettings()) { ID = Guid.NewGuid() }, source);
        else File.WriteAllText(source, "not JSON");
        var service = Service(new DataContext { Tournament = target });

        var result = await service.ImportAsync(source);

        result.Import.Outcome.Should().Be(foreign ? ImportOutcome.TournamentMismatch : ImportOutcome.FileUnavailable);
        Directory.Exists(Path.Combine(_folder, "recovery")).Should().BeFalse();
        File.ReadAllText(target.FileName).Should().Be(original);
        Match(target).Status.Should().Be(MatchStatusEnum.Pending);
        var missing = await service.ImportAsync(Path.Combine(_folder, "missing.wrt"));
        missing.Import.Outcome.Should().Be(ImportOutcome.FileUnavailable);
    }

    [Fact]
    public async Task Backup_failure_prevents_applying_changes()
    {
        var target = MakeTournament();
        var source = SaveRemote(target);
        var blockedRoot = Path.Combine(_folder, "not-a-directory");
        File.WriteAllText(blockedRoot, "block directory creation");

        var result = await Service(new DataContext { Tournament = target }, backupRoot: blockedRoot).ImportAsync(source);

        result.Status.Should().Be(ManualImportStatus.BackupFailed);
        Match(target).Status.Should().Be(MatchStatusEnum.Pending);
    }

    [Fact]
    public async Task Save_failure_is_reported_and_retried_even_when_the_same_file_has_no_new_data()
    {
        var target = MakeTournament();
        var source = SaveRemote(target);
        var manager = new FakeTournamentsManager { SaveAsyncResult = false };
        var service = Service(new DataContext { Tournament = target }, manager: manager);

        var failed = await service.ImportAsync(source);

        failed.Status.Should().Be(ManualImportStatus.SaveFailed);
        Match(target).Status.Should().Be(MatchStatusEnum.Completed);
        manager.SaveAsyncCount.Should().Be(1);
        ConductingViewModel.FormatImportMessage(failed).Should().Contain("не сохранён");
        manager.SaveAsyncResult = true;
        var repeated = await service.ImportAsync(source);
        repeated.Status.Should().Be(ManualImportStatus.Completed);
        repeated.Import.Outcome.Should().Be(ImportOutcome.NoNewData);
        manager.SaveAsyncCount.Should().Be(2);
        repeated.BackupFileName.Should().BeNull();
        repeated.Saved.Should().BeTrue();
        Directory.GetFiles(Path.Combine(_folder, "recovery"), "*.wrt", SearchOption.AllDirectories).Should().ContainSingle();
    }

    [Fact]
    public async Task Conflict_keeps_local_result_and_saves_accepted_independent_field_change()
    {
        var target = MakeTournament();
        var source = SaveRemote(target, redWon: false);
        var remote = _manager.LoadFromFile(source);
        remote.Parts[0].Name = "Обновлённая часть";
        remote.MetaVersion++;
        _manager.SaveToFile(remote, source).Should().BeTrue();
        var processor = new RoundRobinGroupBracketProcessor();
        processor.Load(target, target.Groups[0]);
        Match(target).PointsRed = 2;
        processor.CompleteMatch(Match(target), true, MatchWinTypeEnum.PointsWin);
        Match(target).Version = 1;
        var manager = new FakeTournamentsManager();

        var result = await Service(new DataContext { Tournament = target }, manager: manager).ImportAsync(source);

        result.Import.Outcome.Should().Be(ImportOutcome.Conflict);
        result.Import.Conflicts.Should().ContainSingle();
        result.Import.ImportedCount.Should().BeGreaterThan(0);
        target.Parts[0].Name.Should().Be("Обновлённая часть");
        manager.SaveAsyncCount.Should().Be(1);
        Match(target).IsRedWon.Should().BeTrue();
        ConductingViewModel.FormatImportMessage(result).Should().Contain("конфликты");
    }

    [Fact]
    public async Task Conducting_page_disables_import_while_loading_and_enqueues_the_result()
    {
        var target = MakeTournament();
        var di = TestContainerBuilder.MakeDefault();
        var context = di.Resolve<IDataContext>();
        context.Tournament = target;
        var importer = new WaitingImporter();
        di.Add<ManualTournamentImportService>(new ManualTournamentImportService(context, importer,
            _manager, null, _adapter, _dataAccess, Path.Combine(_folder, "recovery")));
        var vm = new ConductingViewModel(di);
        vm.InitData();
        var operation = vm.ImportFromFileAsync("source.wrt");
        await importer.Started.Task;

        vm.IsFileImportRunning.Should().BeTrue();
        vm.ImportFromFileCommand.CanExecute(null).Should().BeFalse();
        importer.Ready.SetResult(ImportPlan.Skip(ImportOutcome.NoNewData));
        await operation;

        vm.IsFileImportRunning.Should().BeFalse();
        vm.ImportFromFileCommand.CanExecute(null).Should().BeTrue();
        var shell = (FakeShellViewModel)di.Resolve<INavigationService>().ShellVm;
        shell.Snackbar.Should().ContainSingle().Which.Should().Be("Новых данных нет.");
    }

    [Fact]
    public async Task Closing_tournament_during_prepare_does_not_apply_the_prepared_data()
    {
        var target = MakeTournament();
        var context = new DataContext { Tournament = target };
        var importer = new WaitingImporter();
        var operation = Service(context, importer: importer).ImportAsync("source.wrt");
        await importer.Started.Task;
        context.Tournament = null;
        importer.Ready.SetResult(ImportPlan.Proceed(Clone(target)));

        (await operation).Status.Should().Be(ManualImportStatus.TargetChanged);
        importer.Applied.Should().BeFalse();
        Directory.Exists(Path.Combine(_folder, "recovery")).Should().BeFalse();
    }

    [Fact]
    public async Task Unsaved_working_tournament_requires_a_destination_before_import()
    {
        var target = MakeTournament();
        target.FileName = null;
        var importer = new WaitingImporter();

        var result = await Service(new DataContext { Tournament = target }, importer: importer).ImportAsync("source.wrt");

        result.Status.Should().Be(ManualImportStatus.SaveRequired);
        importer.Started.Task.IsCompleted.Should().BeFalse();
    }

    private sealed class WaitingImporter : ITournamentImporter
    {
        public TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<ImportPlan> Ready { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Applied { get; private set; }
        public Task<ImportPlan> PrepareAsync(WTournament target, string fileName, CancellationToken cancellationToken = default)
        {
            Started.TrySetResult(true);
            return Ready.Task;
        }
        public ImportResult Apply(WTournament target, ImportPlan plan) { Applied = true; return ImportResult.Imported(1); }
    }

    private sealed class WaitingBackupAccess : ITournamentDataAccess
    {
        private readonly ITournamentDataAccess _inner;
        public TaskCompletionSource<bool> Written { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public WaitingBackupAccess(ITournamentDataAccess inner) => _inner = inner;
        public Wrestling.Data.TournamentInfo LoadFromFile(string path) => _inner.LoadFromFile(path);
        public Task<Wrestling.Data.TournamentInfo> LoadFromFileAsync(string path, CancellationToken token = default) => _inner.LoadFromFileAsync(path, token);
        public bool SaveToFile(Wrestling.Data.TournamentInfo info, string path) => _inner.SaveToFile(info, path);
        public async Task<bool> SaveToFileAsync(Wrestling.Data.TournamentInfo info, string path, CancellationToken token = default)
        {
            var saved = await _inner.SaveToFileAsync(info, path, token);
            Written.TrySetResult(true);
            await Release.Task.WaitAsync(token);
            return saved;
        }
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);
}
