using System;
using System.Collections.Generic;
using Wrestling.Entities;
using Wrestling.Recorder;
using Wrestling.UI.Material.ScoreScreen;

namespace Wrestling.UI.Material.Utils.Recording
{
    public interface IMatchRecorder
    {
        void DeleteRecording(string storagePath, int matchNumber, Guid? tournamentId);
        void StartRecording(string storagePath, RecorderConfiguration config, ScoreScreenViewModel match, Guid? tournamentId);
        void StopRecording();
        void SetTimerOffset(int t);
        void CreateOverlay(bool flag);
        bool IsRecording { get; }
        void SetMainSecond(int t);
        void SetMaxSeconds(int t);
        IEnumerable<string> GetMatchRecordings(string storagePath, WrestlingMatch match, Guid? tournamentId);

        event EventHandler<string> RecordingCompleted;
    }
}