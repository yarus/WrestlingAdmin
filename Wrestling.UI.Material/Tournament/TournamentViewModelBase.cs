using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Collections.Generic;
using CsvHelper;
using MvvmDialogs.FrameworkDialogs.SaveFile;
using Wrestling.Providers;
using Wrestling.UI.Material.Home;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Utils;
using System.Linq;

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
        
        protected void CloseTournament()
        {
            bool saveRequired = true;

            if (!IsAutosaveEnabled)
            {
                if (Dialog.ShowMessageBox(this,
                        "Автосохранение выключено. Сохранить турнир перед выходом?",
                        "Требуется подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Information) != MessageBoxResult.Yes) saveRequired = false;
            }

            if(saveRequired) SaveData();

            DataContext.Tournament = null;
            DataContext.Group = null;
            DataContext.WrestlingMatch = null;

            NavigateToView<HomeViewModel>();
        }

        protected async void SaveData()
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

                    var result = TournamentManager.SaveToFile(DataContext.Tournament, settings.FileName);
                    ShowSnackMessage(result ? "Турнир сохранен! Автосохранение включено." : "При сохранении произошла ошибка!");
                }
            }
        }

        protected void ExportData()
        {
            if (DataContext.Tournament == null)
            {
                return;
            }

            var settings = new SaveFileDialogSettings
            {
                Title = "Экспортировать участников в файл",
                CheckFileExists = false,
                OverwritePrompt = true,
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Filter = "CSV (*.csv)|*.csv|All Files (*.*)|*.*"
            };

            bool? success = Dialog.ShowSaveFileDialog(this, settings);
            if (success == true)
            {
                try
                {

                    using (var writer = new StreamWriter(settings.FileName))

                    using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
                    {
                        var exportData = DataContext.Tournament.Wrestlers.Select(item =>
                        {
                            return new ExportedWrestler()
                            {
                                FullName = item.FullName,
                                BirthDate = item.BirthDate.HasValue ? item.BirthDate.Value.ToString("dd/MM/yyyy") : string.Empty,
                                FinalPlace = item.FinalPlace.HasValue ? item.FinalPlace.Value.ToString() : string.Empty,
                                GroupName = item.GroupName,
                                TeamCity = item.TeamCity,
                                TeamName = item.TeamName
                            };
                        }).OrderBy(x => x.GroupName).ThenBy(x => x.FinalPlace);

                        csv.WriteRecords(exportData);
                    }

                    ShowSnackMessage("Список участников экспортирован!");
                }
                catch(Exception ex)
                {
                    ShowSnackMessage($"Произошла ошибка экспорта: {ex.Message}");
                }
            }
        }
    }
}

public class ExportedWrestler
{
    public string GroupName { get; set; }
    public string FullName { get; set; }
    public string TeamName { get; set; }
    public string TeamCity { get; set; }
    public string BirthDate { get; set; }
    public string FinalPlace { get; set; }
}