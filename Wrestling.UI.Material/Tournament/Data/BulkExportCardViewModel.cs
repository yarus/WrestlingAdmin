using System.Windows.Input;
using MaterialDesignThemes.Wpf;
using Wrestling.Entities;

namespace Wrestling.UI.Material.Tournament.Data
{
    // One card on the «Данные» screen. Pure data + busy-state + command;
    // commands themselves are constructed by DataViewModel and either call
    // existing exporter helpers (BulkBracketPdfExporter) or wrap them in
    // an AsyncRelayCommand that flips IsBusy while running.
    public sealed class BulkExportCardViewModel : ObservableObject
    {
        private bool _isBusy;

        public BulkExportCardViewModel(string title, string description, PackIconKind iconKind, ICommand executeCommand)
        {
            Title = title;
            Description = description;
            IconKind = iconKind;
            ExecuteCommand = executeCommand;
        }

        public string Title { get; }

        public string Description { get; }

        public PackIconKind IconKind { get; }

        public ICommand ExecuteCommand { get; }

        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (_isBusy == value) return;
                _isBusy = value;
                OnPropertyChanged(nameof(IsBusy));
            }
        }
    }
}
