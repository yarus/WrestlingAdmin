using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;
using MvvmDialogs.FrameworkDialogs.OpenFile;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Tournament.Conducting
{
    public partial class ConductingViewModel
    {
        private ManualTournamentImportService _manualImporter;
        private ICommand _importFromFileCommand;
        private bool _isFileImportRunning;

        public ICommand ImportFromFileCommand => _importFromFileCommand ?? (_importFromFileCommand =
            new AsyncRelayCommand(_ => ChooseImportFileAsync(), _ => Tournament != null && !IsFileImportRunning));

        public bool IsFileImportRunning
        {
            get => _isFileImportRunning;
            private set { _isFileImportRunning = value; OnPropertyChanged(nameof(IsFileImportRunning)); }
        }

        private async Task ChooseImportFileAsync()
        {
            var settings = new OpenFileDialogSettings
            {
                Title = T("ManualImport_Title", "Импорт из файла"),
                Filter = T("ManualImport_FileFilter", "Файлы турнира (*.wrt)|*.wrt"),
                CheckFileExists = true,
                Multiselect = false
            };
            if (Dialog.ShowOpenFileDialog(this, settings) != true) return;
            var target = Tournament;
            if (string.IsNullOrEmpty(target.FileName))
            {
                await SaveDataAsync();
                if (!ReferenceEquals(Tournament, target)) return;
                if (string.IsNullOrEmpty(target.FileName))
                {
                    ShowSnackMessage(T("ManualImport_SaveRequired", "Перед импортом сохраните рабочий турнир."));
                    return;
                }
            }
            await ImportFromFileAsync(settings.FileName);
        }

        internal async Task ImportFromFileAsync(string fileName)
        {
            if (IsFileImportRunning || Tournament == null) return;
            IsFileImportRunning = true;
            try
            {
                var result = await _manualImporter.ImportAsync(fileName);
                if (result.Status == ManualImportStatus.TargetChanged)
                    return;
                ShowSnackMessage(FormatImportMessage(result));
                RefreshAll();
            }
            finally { IsFileImportRunning = false; }
        }

        internal static string FormatImportMessage(ManualImportResult result)
        {
            var lines = new List<string>();
            if (result.Status == ManualImportStatus.SaveRequired)
                lines.Add(T("ManualImport_SaveRequired", "Перед импортом сохраните рабочий турнир."));
            else if (result.Status == ManualImportStatus.BackupFailed)
                lines.Add(T("ManualImport_BackupFailed", "Не удалось создать резервную копию. Импорт не выполнен."));
            else
            {
                var outcome = result.Import.Outcome;
                lines.Add(outcome switch
                {
                    ImportOutcome.Imported => T("ManualImport_Imported", "Изменения импортированы."),
                    ImportOutcome.NoNewData => T("ManualImport_NoNewData", "Новых данных нет."),
                    ImportOutcome.Conflict => T("ManualImport_Conflict", "Обнаружены конфликты."),
                    ImportOutcome.TournamentMismatch => T("ManualImport_Mismatch", "Файл относится к другому турниру. Импорт не выполнен."),
                    ImportOutcome.FileUnavailable => T("ManualImport_Unavailable", "Не удалось прочитать файл. Проверьте путь, доступ и содержимое файла."),
                    _ => T("ManualImport_Error", "Не удалось импортировать данные.")
                });
                if (outcome is ImportOutcome.Imported or ImportOutcome.Conflict)
                    lines.Add(string.Format(T("ManualImport_Count", "Применено изменений: {0}"), result.Import.ImportedCount));
                foreach (var conflict in result.Import.Conflicts)
                {
                    var reason = conflict.Reason switch
                    {
                        ImportConflictReason.DifferentResult => T("PeerSync_Conflict_Result", "Разные результаты одной версии"),
                        ImportConflictReason.DifferentParticipants => T("PeerSync_Conflict_Participants", "Разные участники встречи"),
                        ImportConflictReason.CompletedDependentMatch => T("PeerSync_Conflict_Dependent", "Следующая встреча уже завершена"),
                        ImportConflictReason.InvalidResult => T("PeerSync_Conflict_Invalid", "Неполный результат встречи"),
                        _ => T("PeerSync_Conflict_Structure", "Различается структура сетки")
                    };
                    lines.Add(conflict.GroupName + (conflict.MatchNumber.HasValue ? " №" + conflict.MatchNumber : "") + ": " + reason);
                }
                if (result.Status == ManualImportStatus.SaveFailed)
                    lines.Add(T("ManualImport_SaveFailed", "Изменения приняты, но рабочий турнир не сохранён. Повторите сохранение кнопкой «Сохранить»."));
                else if (result.Saved)
                    lines.Add(T("ManualImport_Saved", "Рабочий турнир сохранён."));
            }
            return string.Join(Environment.NewLine, lines);
        }
    }
}
