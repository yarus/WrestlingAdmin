using System;
using System.Windows.Input;
using MvvmDialogs.FrameworkDialogs.OpenFile;
using Wrestling.UI.Material.ScoreScreen;
using Wrestling.UI.Material.Tournament;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Match
{
    public class MatchSettingsViewModel : TournamentViewModelBase
    {
        private ICommand _setEmblemCommand;
        private ScoreScreenViewModel _scoreScreenVm;

        public MatchSettingsViewModel(IDiContainer container, ScoreScreenViewModel vm) : base(container)
        {
            _scoreScreenVm = vm;
        }

        public ScoreScreenViewModel ScoreScreenVm
        {
            get { return _scoreScreenVm; }
            set
            {
                _scoreScreenVm = value;
                OnPropertyChanged("ScoreScreenVm");
            }
        }

        public ICommand SetEmblemCommand
        {
            get
            {
                if (_setEmblemCommand == null)
                {
                    _setEmblemCommand = new RelayCommand(
                        param => SetEmblem(param.ToString().ToLower() == "red"),
                        param => param != null
                    );
                }
                return _setEmblemCommand;
            }
        }

        private void SetEmblem(bool isRed)
        {
            var settings = new OpenFileDialogSettings
            {
                Title = "Открыть файл с изображением",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Filter = "Изображения (*.png)|*.png|All Files (*.*)|*.*"
            };

            bool? success = Dialog.ShowOpenFileDialog(this, settings);
            if (success == true)
            {
                if (isRed) _scoreScreenVm.Wrestler1TeamEmblem = settings.FileName;
                else _scoreScreenVm.Wrestler2TeamEmblem = settings.FileName;
            }
        }
    }
}