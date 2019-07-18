using Accord.Video.DirectShow;

namespace Wrestling.Recorder.Configurator
{
    public interface ICamView
    {
        void StartPlaying(VideoCaptureDevice device);

        void StopPlaying();
    }
}
