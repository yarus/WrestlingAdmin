using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Wrestling.Entities;
using Wrestling.Providers;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Material.Tests.Fakes;
using Wrestling.UI.Material.Tournament.Import;
using Xunit;

namespace Wrestling.UI.Material.Tests;

// Import path: only a successful Imported outcome should trigger autosave,
// and only when the autosave flag is on. Other outcomes (no new data,
// file unavailable, tournament mismatch, error) must never persist.
//
// Post-split: the importer's heavy Prepare phase must run off the test's
// calling thread (ImportViewModel wraps it in Task.Run). The Apply phase
// runs on the caller's context.
public sealed class ImportAutosaveTests
{
    private sealed class StubImporter : ITournamentImporter
    {
        private readonly ImportResult _result;
        public int PrepareCalls { get; private set; }
        public int ApplyCalls { get; private set; }
        public int PrepareThreadId { get; private set; }
        public int ApplyThreadId { get; private set; }

        public StubImporter(ImportResult result) => _result = result;

        public Task<ImportPlan> PrepareAsync(Entities.Tournament target, string fileName)
        {
            PrepareCalls++;
            PrepareThreadId = Thread.CurrentThread.ManagedThreadId;

            // For short-circuit outcomes the VM skips Apply entirely.
            switch (_result.Outcome)
            {
                case ImportOutcome.FileUnavailable:
                    return Task.FromResult(ImportPlan.Skip(ImportOutcome.FileUnavailable));
                case ImportOutcome.TournamentMismatch:
                    return Task.FromResult(ImportPlan.Skip(ImportOutcome.TournamentMismatch));
                case ImportOutcome.Error:
                    return Task.FromResult(ImportPlan.Skip(ImportOutcome.Error));
                default:
                    // Give Apply something to work against; the stub's Apply
                    // ignores contents and returns the configured result.
                    var remote = new Entities.Tournament(new GlobalSettings()) { Name = target.Name };
                    return Task.FromResult(ImportPlan.Proceed(remote));
            }
        }

        public ImportResult Apply(Entities.Tournament target, ImportPlan plan)
        {
            ApplyCalls++;
            ApplyThreadId = Thread.CurrentThread.ManagedThreadId;
            return _result;
        }
    }

    private static (ImportViewModel vm, FakeTournamentsManager mgr, StubImporter importer)
        BuildVm(ImportResult result, bool autosave)
    {
        var di = TestContainerBuilder.MakeDefault();
        var settings = new GlobalSettings { IsAutosaveEnabled = autosave };
        var tournament = new Entities.Tournament(settings)
        {
            FileName = "tournament.wrt",
            Name = "T",
            ImportSources = new ObservableCollection<string>()
        };
        di.Resolve<IDataContext>().Tournament = tournament;

        var importer = new StubImporter(result);
        di.Add<ITournamentImporter>(importer);

        var vm = new ImportViewModel(di);
        vm.InitData();

        var mgr = (FakeTournamentsManager)di.Resolve<ITournamentsManager>();
        return (vm, mgr, importer);
    }

    [Fact]
    public async Task Imported_outcome_with_autosave_on_triggers_one_save()
    {
        var (vm, mgr, importer) = BuildVm(ImportResult.Imported(3), autosave: true);

        await vm.ImportDataAsync("peer.wrt");

        importer.PrepareCalls.Should().Be(1);
        importer.ApplyCalls.Should().Be(1);
        mgr.SaveAsyncCount.Should().Be(1);
    }

    [Fact]
    public async Task Prepare_runs_off_callers_thread_to_keep_UI_responsive()
    {
        var callerThreadId = Thread.CurrentThread.ManagedThreadId;
        var (vm, _, importer) = BuildVm(ImportResult.Imported(1), autosave: false);

        await vm.ImportDataAsync("peer.wrt");

        importer.PrepareThreadId.Should().NotBe(callerThreadId,
            "the heavy load + parse + adapter step must run off the UI thread via Task.Run");
    }

    [Fact]
    public async Task Short_circuit_outcome_never_invokes_Apply()
    {
        // FileUnavailable is short-circuited inside PrepareAsync; the VM must
        // not call Apply for these outcomes because there is nothing to merge.
        var (vm, _, importer) = BuildVm(ImportResult.FileUnavailable(), autosave: true);

        await vm.ImportDataAsync("peer.wrt");

        importer.PrepareCalls.Should().Be(1);
        importer.ApplyCalls.Should().Be(0);
    }

    [Fact]
    public async Task Imported_outcome_with_autosave_off_does_not_save()
    {
        var (vm, mgr, _) = BuildVm(ImportResult.Imported(3), autosave: false);

        await vm.ImportDataAsync("peer.wrt");

        mgr.SaveAsyncCount.Should().Be(0);
    }

    [Theory]
    [InlineData(ImportOutcome.NoNewData)]
    [InlineData(ImportOutcome.FileUnavailable)]
    [InlineData(ImportOutcome.TournamentMismatch)]
    [InlineData(ImportOutcome.Error)]
    public async Task Non_imported_outcome_never_saves_even_with_autosave_on(ImportOutcome outcome)
    {
        var result = outcome switch
        {
            ImportOutcome.NoNewData => ImportResult.NoNewData(),
            ImportOutcome.FileUnavailable => ImportResult.FileUnavailable(),
            ImportOutcome.TournamentMismatch => ImportResult.TournamentMismatch(),
            _ => ImportResult.Error()
        };

        var (vm, mgr, _) = BuildVm(result, autosave: true);

        await vm.ImportDataAsync("peer.wrt");

        mgr.SaveAsyncCount.Should().Be(0);
    }
}
