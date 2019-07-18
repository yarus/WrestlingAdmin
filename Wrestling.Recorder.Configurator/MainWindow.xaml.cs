using System.ComponentModel;
using System.Threading;
using System.Windows;
using Accord.Video;
using Accord.Video.DirectShow;
using Wrestling.DataAccess;
using Wrestling.UI.Material.Utils.Recording;

namespace Wrestling.Recorder.Configurator
{
    public partial class MainWindow : Window, ICamView
    {
        private MainViewModel _vm;

        public MainWindow()
        {
            InitializeComponent();
        }
  
        private void MainWindow_OnClosing(object sender, CancelEventArgs e)
        {
            _vm?.StopPlaying();
        }

        private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            _vm = new MainViewModel(this, new RecorderConfigurationDataAccess(new JsonStorageDataAccess()));

            DataContext = _vm;
        }

        public void StartPlaying(VideoCaptureDevice device)
        {
            // start new video source
            VideoSourcePlayer.NewFrameReceived += VideoSourcePlayer_NewFrameReceived;
            VideoSourcePlayer.VideoSource = new AsyncVideoSource(device);
            VideoSourcePlayer.Start();
        }

        public void StopPlaying()
        {
            // stop current video source
            VideoSourcePlayer.SignalToStop();
            VideoSourcePlayer.WaitForStop();

            // wait 2 seconds until camera stops
            for (int i = 0; i < 50 && VideoSourcePlayer.IsRunning; i++)
                Thread.Sleep(100);

            if (VideoSourcePlayer.IsRunning)
                VideoSourcePlayer.Stop();
        }

        private void VideoSourcePlayer_NewFrameReceived(object sender, Accord.Video.NewFrameEventArgs eventArgs)
        {
        }
    }
}