using System;
using System.Threading.Tasks;
using System.Windows;
using MvvmDialogs.FrameworkDialogs.SaveFile;
using Wrestling.Providers;
using Wrestling.UI.Material.Home;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Tournament
{
    public abstract class TournamentViewModelBase : ViewModelBase
    {
        private ITournamentsManager _tournService;

        protected TournamentViewModelBase(IDiContainer container) : base(container)
        {
        }

        public override void InitData()
        {
            base.InitData();

            _tournService = Resolve<ITournamentsManager>();

            OnPropertyChanged("IsAutosaveEnabled");
        }

        protected ITournamentsManager TournamentManager => _tournService;

        public Entities.Tournament Tournament => DataContext.Tournament;

        public bool IsAutosaveEnabled => DataContext.Tournament?.Settings.IsAutosaveEnabled ?? false;
        
        protected async Task CloseTournament()
        {
            bool saveRequired = true;

            if (!IsAutosaveEnabled)
            {
                if (Dialog.ShowMessageBox(this,
                        "Автосохранение выключено. Сохранить турнир перед выходом?",
                        "Требуется подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Information) != MessageBoxResult.Yes) saveRequired = false;
            }

            if(saveRequired) await SaveDataAsync();

            DataContext.Tournament = null;
            DataContext.Group = null;
            DataContext.WrestlingMatch = null;

            NavigateToView<HomeViewModel>();
        }

        protected async Task SaveDataAsync()
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
                    DataContext.Tournament.Settings.IsAutosaveEnabled = true;
                    DataContext.Tournament.Settings.AutosaveMaxSecond = GlobalSettings.AutosaveMaxSecond;

                    var result = await TournamentManager.SaveToFileAsync(DataContext.Tournament, settings.FileName);
                    ShowSnackMessage(result ? "Турнир сохранен! Автосохранение включено." : "При сохранении произошла ошибка!");
                }
            }
        }

        // Event-driven autosave hook. Call after any in-memory state change
        // that should be persisted (match completion, successful import).
        // No-op when autosave is disabled — users in manual-save mode rely on
        // the "Сохранить турнир" quick button on the dashboard instead.
        public async Task SaveIfAutosaveEnabledAsync()
        {
            if (IsAutosaveEnabled && DataContext.Tournament != null)
            {
                await SaveDataAsync();
            }
        }
    }
}