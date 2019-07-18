using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Input;
using System.Windows.Threading;
using Wrestling.Entities;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Utils;
using Wrestling.UI.Material.Match;
using Wrestling.UI.Material.Utils.Recording;

namespace Wrestling.UI.Material.ReplayScreen
{
    public class ReplayScreenViewModel : ViewModelBase
    {
        #region Fields

        private ICommand _openFileCommand;
        private readonly IMatchRecorder _recorder;
        private ObservableCollection<string> _videos;
        private string _selectedVideo;
        private DispatcherTimer _timer;
        private string _recordingsLabel;

        #endregion

        public ReplayScreenViewModel(IDiContainer container) : base(container)
        {
            _recorder = Resolve<IMatchRecorder>();
        }

        public override bool IsBackButtonAvailable => true;
        public override string PageTitle => "Запись";

        public ICommand OpenFileCommand
        {
            get
            {
                if (_openFileCommand == null)
                {
                    _openFileCommand = new RelayCommand(param => OpenFile(param.ToString()), param => param != null);
                }
                return _openFileCommand;
            }
        }

        private void OpenFile(string path)
        {
            if (File.Exists(path))
            {
                Process.Start(path);
            }
        }

        protected override void OnBackCommand()
        {
            _timer?.Stop();

            base.OnBackCommand();

            if (DataContext.WrestlingMatch.IsMatchCompleted)
            {
                NavigateToView<MatchResultsViewModel>();
            }
            else
            {
                NavigateToView<MatchControlViewModel>();
            }
        }

        public override void InitData()
        {
            base.InitData();

            RecordingsLabel = "Видеозаписи";

            Videos = new ObservableCollection<string>();

            if (DataContext.WrestlingMatch != null)
            {
                LoadRecordings();

                StartRetryTimer();
            }
        }

        private void LoadRecordings()
        {
            var storagePath = DataContext.Tournament != null ? DataContext.Tournament.Settings.VideoStoragePath : GlobalSettings.VideoStoragePath;

            var files = _recorder.GetMatchRecordings(storagePath, DataContext.WrestlingMatch, DataContext.Tournament?.ID).ToList();

            Videos = new ObservableCollection<string>(files);
        }

        private void StartRetryTimer()
        {
            _timer?.Stop();

            _timer = new DispatcherTimer();
            _timer.Tick += TimerOnTick;
            _timer.Interval = new TimeSpan(0, 0, 0, 5);
            _timer.Start();
        }

        private void TimerOnTick(object sender, EventArgs e)
        {
            LoadRecordings();
        }

        public WrestlingMatch WrestlingMatch
        {
            get { return DataContext.WrestlingMatch; }
            set
            {
                DataContext.WrestlingMatch = value;

                OnPropertyChanged("WrestlingMatch");
            }
        }

        public ObservableCollection<string> Videos
        {
            get { return _videos; }
            set
            {
                _videos = value;

                if (string.IsNullOrEmpty(SelectedVideo) && _videos.Count > 0)
                {
                    SelectedVideo = _videos[0];
                }

                OnPropertyChanged("Videos");
            }
        }

        public string SelectedVideo
        {
            get { return _selectedVideo; }
            set
            {
                _selectedVideo = value;
                OnPropertyChanged("SelectedVideo");
            }
        }

        public string RecordingsLabel
        {
            get { return _recordingsLabel; }
            set
            {
                _recordingsLabel = value;
                OnPropertyChanged("RecordingsLabel");
            }
        }
    }
}