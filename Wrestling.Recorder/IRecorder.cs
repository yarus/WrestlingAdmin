using System;
using System.Windows.Media.Imaging;

namespace Wrestling.Recorder
{
    public interface IRecorder
    {
        bool IsRecording { get; }
        DateTime RecordingStartTime { get; }

        void Dispose();
        void StartRecording();
        void StopRecording();
        void SetTimerOffset(int t);
        void CreateOverlay(bool flag);
        void SetMainSecond(int t);
        void SetMaxSeconds(int seconds);

        event EventHandler<FrameGeneratedEventArgs> NewFrame;
        event EventHandler<string> RecordingStarted;
        event EventHandler<string> RecordingCompleted;
        event EventHandler<Exception> RecordingException;
        event EventHandler<BitmapSource> FrameShow;
    }
}