using System.Collections.ObjectModel;
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
public sealed class ImportAutosaveTests
{
    private sealed class StubImporter : ITournamentImporter
    {
        private readonly ImportResult _result;
        public int Calls { get; private set; }
        public StubImporter(ImportResult result) => _result = result;
        public Task<ImportResult> ImportDataFromFileAsync(Entities.Tournament target, string fileName)
        {
            Calls++;
            return Task.FromResult(_result);
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

        importer.Calls.Should().Be(1);
        mgr.SaveAsyncCount.Should().Be(1);
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
