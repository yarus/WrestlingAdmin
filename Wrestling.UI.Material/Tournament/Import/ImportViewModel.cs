using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using MaterialDesignThemes.Wpf;
using MvvmDialogs.FrameworkDialogs.OpenFile;
using Wrestling.Providers.Network;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Material.Tournament.Dashboard;
using Wrestling.UI.Utils;
using static System.Windows.Forms.DataFormats;

namespace Wrestling.UI.Material.Tournament.Import
{
    public class ImportViewModel : TournamentViewModelBase
    {
        #region Fields

        private IList<CommandButtonItem> _quickButtons;

        private string _path;
        private bool _isValid;

        private bool _isImportJobStarted;
        private bool _isLoading;
        private int _importSeconds;
        private int _currentSecond;
        private TimeSpan _leftToImport;

        private DispatcherTimer _timer;
        
        private ITournamentImporter _importer;

        private ObservableCollection<string> _importLog;
        
        private ICommand _startImportJobCommand;
        private ICommand _endImportJobCommand;
        private ICommand _selectPathCommand;
        private ICommand _deletePathCommand;
        private ICommand _addPathCommand;
        private ICommand _addDiscoveredPeerCommand;
        private ICommand _addAllDisplayedPeersCommand;

        private IPeerDiscoveryService _discovery;
        private bool _discoveryWired;
        private Dispatcher _uiDispatcher;
        private ObservableCollection<DiscoveredPeer> _discoveredPeers;

        #endregion

        public ImportViewModel(IDiContainer container) : base(container)
        {            
        }

        public override IList<CommandButtonItem> QuickButtons
        {
            get
            {
                return _quickButtons ?? (_quickButtons = new List<CommandButtonItem>
                {
                    new CommandButtonItem("Импортировать результаты из файла", PackIconKind.FileImport,
                    new AsyncRelayCommand(
                        execute: async _ => {
                            IsLoading = true;
                            try
                            {
                                await RunManualImportAsync();
                            }
                            finally
                            {
                                IsLoading = false;
                            }
                        }
                    ))
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

        public bool IsLoading
        {
            get { return _isLoading; }
            set
            {
                _isLoading = value;
                OnPropertyChanged("IsLoading");
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

        public ObservableCollection<string> ImportSources
        {
            get { return DataContext.Tournament.ImportSources; }
            set
            {
                DataContext.Tournament.ImportSources = value;
                OnPropertyChanged("ImportSources");
            }
        }

        public ObservableCollection<DiscoveredPeer> DiscoveredPeers
        {
            get { return _discoveredPeers; }
            private set
            {
                _discoveredPeers = value;
                OnPropertyChanged("DiscoveredPeers");
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

            WireDiscovery();
        }

        private void WireDiscovery()
        {
            // Singleton VM — we can only afford to subscribe once per process
            // lifetime, otherwise repeat navigations leak event handlers and
            // add duplicate peers on every incoming packet.
            if (_discoveryWired) return;

            _discovery = Resolve<IPeerDiscoveryService>();
            if (_discovery == null) return;

            _uiDispatcher = Dispatcher.CurrentDispatcher;
            _discoveredPeers = new ObservableCollection<DiscoveredPeer>();
            OnPropertyChanged("DiscoveredPeers");

            // Seed with anything the service already knows — discovery runs
            // since the tournament was opened, possibly well before the user
            // navigated here.
            foreach (var peer in _discovery.SnapshotPeers())
            {
                _discoveredPeers.Add(peer);
            }

            _discovery.PeerUpserted += OnPeerUpserted;
            _discovery.PeerExpired += OnPeerExpired;
            _discoveryWired = true;
        }

        private void OnPeerUpserted(object sender, DiscoveredPeer peer)
        {
            RunOnUi(() =>
            {
                if (!_discoveredPeers.Contains(peer))
                {
                    _discoveredPeers.Add(peer);
                }
            });
        }

        private void OnPeerExpired(object sender, DiscoveredPeer peer)
        {
            RunOnUi(() => _discoveredPeers.Remove(peer));
        }

        private void RunOnUi(Action action)
        {
            if (_uiDispatcher == null || _uiDispatcher.CheckAccess()) action();
            else _uiDispatcher.BeginInvoke(action);
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
                    _selectPathCommand = new AsyncRelayCommand(
                        execute: async _ => await SelectPathAsync()
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

        public ICommand AddDiscoveredPeerCommand
        {
            get
            {
                if (_addDiscoveredPeerCommand == null)
                {
                    _addDiscoveredPeerCommand = new RelayCommand(
                        param => AddDiscoveredPeer(param as DiscoveredPeer),
                        param => CanAddDiscoveredPeer(param as DiscoveredPeer)
                    );
                }
                return _addDiscoveredPeerCommand;
            }
        }

        public ICommand AddAllDisplayedPeersCommand
        {
            get
            {
                if (_addAllDisplayedPeersCommand == null)
                {
                    _addAllDisplayedPeersCommand = new RelayCommand(
                        param => AddAllDisplayedPeers(),
                        param => true
                    );
                }
                return _addAllDisplayedPeersCommand;
            }
        }

        #endregion

        #region Private Methods
        
        private async Task OnTimerTickAsync()
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
                try
                {
                    IsLoading = true;

                    foreach (var path in ImportSources)
                    {
                        await ImportDataAsync(path);
                    }
                }
                finally
                {
                    IsLoading = false;
                }                

                _currentSecond = 0;
            }

            _currentSecond++;
        }

        private async void OnTimerTick(object sender, EventArgs e)
        {
            _timer.Stop(); // Pause timer during execution

            try
            {
                await OnTimerTickAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                // Handle errors
                Debug.WriteLine($"Import failed: {ex.Message}");
            }
            finally
            {
                if (IsImportJobStarted) // Only restart if still enabled
                    _timer.Start();
            }
        }

        private void StartImportJob()
        {
            if (_timer == null)
            {
                _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                _timer.Tick += OnTimerTick;
            }
            else
            {
                _timer.Stop();
            }

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

        private async Task RunManualImportAsync()
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
                var tournament = await TournamentManager.LoadFromFileAsync(settings.FileName);
                if (tournament != null && tournament.Name == DataContext.Tournament.Name)
                {
                    await ImportDataAsync(settings.FileName);
                }
                else
                {
                    ShowSnackMessage("Выбран файл, не соответствующий открытому турниру!");
                }
            }
        }

        private void AddPath(string path)
        {
            ImportSources.Add(path);

            Path = string.Empty;
            IsValid = false;
        }

        private void DeletePath(string path)
        {
            var setting = ImportSources.FirstOrDefault(s => s == path);
            if (!string.IsNullOrEmpty(setting))
            {
                ImportSources.Remove(setting);
                OnPropertyChanged("ImportSources");
            }
        }

        private async Task SelectPathAsync()
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
                if (ImportSources.FirstOrDefault(s => s == settings.FileName) != null)
                {
                    ShowSnackMessage("Файл с таким именем уже добавлен в список для импорта!");
                    IsValid = false;
                    Path = string.Empty;
                    return;
                }

                try
                {
                    var tournament = await TournamentManager.LoadFromFileAsync(settings.FileName)
                        .ConfigureAwait(true);  // Continue on UI context for property updates

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
                catch (Exception ex)
                {
                    // Handle any errors
                    Path = string.Empty;
                    IsValid = false;
                    ShowSnackMessage($"Ошибка загрузки файла: {ex.Message}");
                }
            }
        }

        // internal for test access. Runs the expensive load + parse + adapter
        // step on a threadpool thread via Task.Run so a slow tick cannot stutter
        // the UI (e.g. a live match timer's render loop). The apply phase runs
        // on the captured (UI) context and is small — typically < 10 ms because
        // only the matches that actually flipped Pending→Completed since the
        // last import touch ObservableCollections.
        internal async Task ImportDataAsync(string path)
        {
            var target = DataContext.Tournament;

            ImportPlan plan;
            try
            {
                plan = await Task.Run(() => _importer.PrepareAsync(target, path)).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Import prepare failed: {ex.Message}");
                AddLog(path, "Ошибка импорта. Подробности в журнале.");
                return;
            }

            ImportResult result;
            try
            {
                result = plan.NeedsApply
                    ? _importer.Apply(target, plan)
                    : new ImportResult(plan.ShortCircuit ?? ImportOutcome.Error, 0);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Import apply failed: {ex.Message}");
                AddLog(path, "Ошибка импорта. Подробности в журнале.");
                return;
            }

            switch (result.Outcome)
            {
                case ImportOutcome.Imported:
                    AddLog(path, $"Успешно загружено {result.ImportedCount} результатов!");
                    ShowSnackMessage($"Успешно импортировано {result.ImportedCount} результатов!");
                    await SaveIfAutosaveEnabledAsync();
                    break;
                case ImportOutcome.NoNewData:
                    AddLog(path, "Новые данные отсутствуют!");
                    break;
                case ImportOutcome.FileUnavailable:
                    AddLog(path, "Файл недоступен (сеть или путь). Подробности в журнале.");
                    break;
                case ImportOutcome.TournamentMismatch:
                    AddLog(path, "Файл не соответствует текущему турниру.");
                    break;
                default:
                    AddLog(path, "Ошибка импорта. Подробности в журнале.");
                    break;
            }
        }

        // When a peer advertises both HTTP and UNC we pack them into a single
        // ImportSources entry separated by the importer's alternatives char.
        // The importer tries HTTP first (usually works out of the box) and
        // falls back to UNC automatically if the embedded HTTP server is down
        // or blocked by a firewall.
        private static string PeerPreferredSource(DiscoveredPeer peer)
        {
            if (peer == null) return null;
            var hasHttp = !string.IsNullOrEmpty(peer.HttpUrl);
            var hasUnc = !string.IsNullOrEmpty(peer.UncPath);
            if (hasHttp && hasUnc) return peer.HttpUrl + TournamentImporter.SourceAlternativesSeparator + peer.UncPath;
            if (hasHttp) return peer.HttpUrl;
            if (hasUnc) return peer.UncPath;
            return null;
        }

        private bool CanAddDiscoveredPeer(DiscoveredPeer peer)
        {
            var src = PeerPreferredSource(peer);
            if (src == null) return false;
            if (DataContext?.Tournament == null) return false;
            return !ImportSources.Contains(src);
        }

        private void AddDiscoveredPeer(DiscoveredPeer peer)
        {
            var src = PeerPreferredSource(peer);
            if (src == null) return;
            if (ImportSources.Contains(src)) return;
            ImportSources.Add(src);
        }

        private void AddAllDisplayedPeers()
        {
            if (_discoveredPeers == null) return;
            foreach (var peer in _discoveredPeers.ToList())
            {
                AddDiscoveredPeer(peer);
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