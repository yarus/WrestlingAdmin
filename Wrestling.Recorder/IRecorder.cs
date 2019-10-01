using System;

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

        event EventHandler<FrameGeneratedEventArgs> NewFrame;
    }
}