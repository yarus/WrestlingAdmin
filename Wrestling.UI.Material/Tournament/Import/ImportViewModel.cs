using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using System.Windows.Threading;
using MaterialDesignThemes.Wpf;
using MvvmDialogs.FrameworkDialogs.OpenFile;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Material.Tournament.Dashboard;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Tournament.Import
{
    public class ImportViewModel : TournamentViewModelBase
    {
        #region Fields

        private IList<CommandButtonItem> _quickButtons;

        private string _path;
        private bool _isValid;

        private bool _isImportJobStarted;
        private int _importSeconds;
        private int _currentSecond;
        private TimeSpan _leftToImport;

        private DispatcherTimer _timer;

        private ObservableCollection<string> _importSettings;
        
        private ITournamentImporter _importer;

        private ObservableCollection<string> _importLog;
        
        private ICommand _startImportJobCommand;
        private ICommand _endImportJobCommand;
        private ICommand _selectPathCommand;
        private ICommand _deletePathCommand;
        private ICommand _addPathCommand;

        #endregion

        public ImportViewModel(IDiContainer container) : base(container)
        {
            _importSettings = new ObservableCollection<string>();
        }

        public override IList<CommandButtonItem> QuickButtons
        {
            get
            {
                return _quickButtons ?? (_quickButtons = new List<CommandButtonItem>
                {
                    new CommandButtonItem("Импортировать результаты из файла", PackIconKind.FileImport, new RelayCommand(param => RunManualImport(), param => true))
                });
            }
        }

        public override string PageTitle => "Импорт Результатов";

        #region Binding Properties

        public override bool IsBackButtonAvailable => true;

        public bool IsImportJobStarted
        {
            get { return _isImportJobStarted; }
            set
            {
                _isImportJobStarted = value;

                OnPropertyChanged("IsImportJobStarted");
                OnPropertyChanged("IsImportJobStoped");
            }
        }

        public bool IsValid
        {
            get { return _isValid; }
            set
            {
                _isValid = value;

                OnPropertyChanged("IsValid");
            }
        }

        public string Path
        {
            get { return _path; }
            set
            {
                _path = value;

                OnPropertyChanged("Path");
            }
        }

        public bool IsImportJobStoped => !_isImportJobStarted;

        public int ImportSeconds
        {
            get { return _importSeconds; }
            set
            {
                _importSeconds = value;

                LeftToImport = new TimeSpan(0, 0, 0, _importSeconds);

                OnPropertyChanged("ImportSeconds");
            }
        }

        public ObservableCollection<string> ImportLog
        {
            get { return _importLog; }
            set
            {
                _importLog = value;

                OnPropertyChanged("ImportLog");
            }
        }
        
        public TimeSpan LeftToImport
        {
            get { return _leftToImport;}
            set
            {
                _leftToImport = value;

                OnPropertyChanged("LeftToImport");
            }
        }

        public ObservableCollection<string> ImportSettings
        {
            get { return _importSettings; }
            set
            {
                _importSettings = value;
                OnPropertyChanged("ImportSettings");
            }
        }

        #endregion

        public override void InitData()
        {
            base.InitData();

            _importer = Resolve<ITournamentImporter>();

            if (DataContext.Tournament == null)
            {
                throw new ApplicationException("Tournament property is not set!");
            }
            
            if (_importLog == null)
            {
                _importLog = new ObservableCollection<string>();
            }

            if (ImportSeconds == 0) ImportSeconds = 300;

            LeftToImport = new TimeSpan(0, 0, 0, ImportSeconds);
        }

        protected override void OnBackCommand()
        {
            NavigateToView<DashboardViewModel>();
        }

        #region Command Properties

        public ICommand SelectPathCommand
        {
            get
            {
                if (_selectPathCommand == null)
                {
                    _selectPathCommand = new RelayCommand(
                        param => SelectPath()
                    );
                }
                return _selectPathCommand;
            }
        }

        public ICommand DeletePathCommand
        {
            get
            {
                if (_deletePathCommand == null)
                {
                    _deletePathCommand = new RelayCommand(
                        param => DeletePath(param?.ToString()),
                        param => param != null
                    );
                }
                return _deletePathCommand;
            }
        }

        public ICommand AddPathCommand
        {
            get
            {
                if (_addPathCommand == null)
                {
                    _addPathCommand = new RelayCommand(
                        param => AddPath(Path),
                        param => IsValid
                    );
                }
                return _addPathCommand;
            }
        }

        public ICommand StartImportJobCommand
        {
            get
            {
                if (_startImportJobCommand == null)
                {
                    _startImportJobCommand = new RelayCommand(
                        param => StartImportJob()
                    );
                }
                return _startImportJobCommand;
            }
        }

        public ICommand EndImportJobCommand
        {
            get
            {
                if (_endImportJobCommand == null)
                {
                    _endImportJobCommand = new RelayCommand(
                        param => EndImportJob(),
                        param => true
                    );
                }
                return _endImportJobCommand;
            }
        }

        #endregion

        #region Private Methods
        
        private void OnTimerTick(object sender, EventArgs e)
        {
            if (DataContext.Tournament == null)
            {
                EndImportJob();
                return;
            }

            var secondsLeft = ImportSeconds - _currentSecond;
            LeftToImport = new TimeSpan(0, 0, 0, secondsLeft > 0 ? secondsLeft : 0);

            if (_currentSecond >= ImportSeconds)
            {
                foreach (var path in ImportSettings)
                {
                    ImportData(path);
                }

                _currentSecond = 0;
            }

            _currentSecond++;
        }

        private void StartImportJob()
        {
            _timer?.Stop();

            _timer = new DispatcherTimer();
            _timer.Tick += OnTimerTick;
            _timer.Interval = new TimeSpan(0, 0, 0, 1);
            _timer.Start();
            IsImportJobStarted = true;
        }

        private void EndImportJob()
        {
            _timer?.Stop();
            _currentSecond = 0;
            IsImportJobStarted = false;
            LeftToImport = new TimeSpan(0, 0, 0, ImportSeconds);
        }

        private void RunManualImport()
        {
            var settings = new OpenFileDialogSettings
            {
                Title = "Импорт результатов поединков",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Filter = "Wrestling Tournament (*.wrt)|*.wrt|All Files (*.*)|*.*"
            };

            bool? success = Dialog.ShowOpenFileDialog(this, settings);
            if (success == true)
            {
                var tournament = TournamentManager.LoadFromFile(settings.FileName);
                if (tournament != null && tournament.Name == DataContext.Tournament.Name)
                {
                    ImportData(settings.FileName);
                }
                else
                {
                    ShowSnackMessage("Выбран файл, не соответствующий открытому турниру!");
                }
            }
        }

        private void AddPath(string path)
        {
            _importSettings.Add(path);

            Path = string.Empty;
            IsValid = false;
        }

        private void DeletePath(string path)
        {
            var setting = _importSettings.FirstOrDefault(s => s == path);
            if (!string.IsNullOrEmpty(setting))
            {
                _importSettings.Remove(setting);
                OnPropertyChanged("ImportSettings");
            }
        }

        private void SelectPath()
        {
            var settings = new OpenFileDialogSettings
            {
                Title = "Открыть файл для импорта",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Filter = "Wrestling Tournament (*.wrt)|*.wrt|All Files (*.*)|*.*"
            };

            bool? success = Dialog.ShowOpenFileDialog(this, settings);
            if (success == true)
            {
                if (_importSettings.FirstOrDefault(s => s == settings.FileName) != null)
                {
                    ShowSnackMessage("Файл с таким именем уже добавлен в список для импорта!");
                    IsValid = false;
                    Path = string.Empty;
                    return;
                }

                var tournament = TournamentManager.LoadFromFile(settings.FileName);
                if (tournament != null && tournament.Name == DataContext.Tournament.Name)
                {
                    Path = settings.FileName;
                    IsValid = true;
                }
                else
                {
                    Path = string.Empty;
                    IsValid = false;
                    ShowSnackMessage("Выбран файл, не соответствующий открытому турниру!");
                }
            }
        }

        private void ImportData(string path)
        {
            int importedRecords = _importer.ImportDataFromFile(DataContext.Tournament, path);

            if (importedRecords > 0)
            {
                AddLog(path, $"Успешно загружено {importedRecords} результатов!");
                ShowSnackMessage($"Успешно импортировано {importedRecords} результатов!");
            }
            else if (importedRecords == 0)
            {
                AddLog(path, "Новые данные отсутствуют!");
            }
            else
            {
                AddLog(path, "Ошибка импорта!");
            }
        }

        private void AddLog(string path, string message)
        {
            if (ImportLog.Count > 10) ImportLog = new ObservableCollection<string>();

            ImportLog.Add(string.Format($"{DateTime.Now} - {path} - {message}"));
        }

        #endregion
    }
}