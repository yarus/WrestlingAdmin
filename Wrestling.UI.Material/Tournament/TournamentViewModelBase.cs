using System;
using System.Threading.Tasks;
using MvvmDialogs.FrameworkDialogs.SaveFile;
using Wrestling.Providers;
using Wrestling.UI.Material.Home;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Material.Slider;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Tournament
{
    public abstract class TournamentViewModelBase : ViewModelBase
    {
        private ITournamentsManager _tournService;
        private IResultsService _resultsService;
        private ISliderWindowManager _sliderWindowManager;

        protected TournamentViewModelBase(IDiContainer container) : base(container)
        {
        }

        public override void InitData()
        {
            base.InitData();

            // Cache infrastructure dependencies once. Per repo convention
            // (CLAUDE.md, Wave 5.2 audit) all DI lookups happen in InitData;
            // method-time Resolve<T> hides dependencies and complicates testing.
            _tournService = Resolve<ITournamentsManager>();
            _resultsService = Resolve<IResultsService>();
            _sliderWindowManager = Resolve<ISliderWindowManager>();
        }

        protected ITournamentsManager TournamentManager => _tournService;
        protected IResultsService ResultsService => _resultsService;
        protected ISliderWindowManager SliderWindowManager => _sliderWindowManager;

        public Entities.Tournament Tournament => DataContext.Tournament;

        protected async Task CloseTournament()
        {
            await SaveDataAsync();

            // Close any open slider windows before dropping the tournament so
            // they don't hold stale references; the score screen stays registered
            // as a singleton and is simply hidden by its own CloseScreen() on next
            // navigation.
            _sliderWindowManager?.CloseAll();

            DataContext.Tournament = null;
            DataContext.Group = null;
            DataContext.WrestlingMatch = null;

            _resultsService?.Recalculate(null);

            NavigateToView<HomeViewModel>();
        }

        public async Task SaveDataAsync()
        {
            if (!string.IsNullOrEmpty(DataContext.Tournament.FileName))
            {
                var result = await TournamentManager.SaveToFileAsync(DataContext.Tournament, DataContext.Tournament.FileName);
                ShowSnackMessage(result ? "Турнир сохранен!" : "При сохранении произошла ошибка!");
            }
            else
            {
                var settings = new SaveFileDialogSettings
                {
                    Title = "Сохранить турнир",
                    CheckFileExists = false,
                    OverwritePrompt = true,
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    Filter = "Wrestling Tournament (*.wrt)|*.wrt|All Files (*.*)|*.*"
                };

                bool? success = Dialog.ShowSaveFileDialog(this, settings);
                if (success == true)
                {
                    var result = await TournamentManager.SaveToFileAsync(DataContext.Tournament, settings.FileName);
                    ShowSnackMessage(result ? "Турнир сохранен!" : "При сохранении произошла ошибка!");
                }
            }
        }

        // Event-driven autosave hook. Call after any in-memory state change
        // that should be persisted (match completion, successful import,
        // peer-sync merge). No-op when no tournament is loaded or the file
        // has never been saved (no FileName) — the operator must pick a path
        // via the "Сохранить турнир" quick button on the dashboard first.
        // We deliberately do NOT pop a SaveAs dialog here: this hook fires on
        // background events (timer-driven imports, match approvals) and a
        // modal dialog mid-tournament would block the operator.
        public async Task SaveIfAutosaveEnabledAsync()
        {
            if (DataContext.Tournament == null) return;
            if (string.IsNullOrEmpty(DataContext.Tournament.FileName)) return;

            var result = await TournamentManager.SaveToFileAsync(DataContext.Tournament, DataContext.Tournament.FileName);
            ShowSnackMessage(result ? "Турнир сохранен!" : "При сохранении произошла ошибка!");
        }
    }
}