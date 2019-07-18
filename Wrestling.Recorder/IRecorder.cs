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

        event EventHandler<FrameGeneratedEventArgs> NewFrame;
    }
}