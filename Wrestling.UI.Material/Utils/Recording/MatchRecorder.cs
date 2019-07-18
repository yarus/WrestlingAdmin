using System;
using System.Collections.Generic;
using System.IO;
using Wrestling.Entities;
using Wrestling.Recorder;
using Wrestling.UI.Material.ScoreScreen;
using Wrestling.UI.Material.Utils.Recording.OverlayDrawer;

namespace Wrestling.UI.Material.Utils.Recording
{
    public class MatchRecorder : IMatchRecorder
    {
        private const string DEFAULT_EXTENSION = ".mp4";
        private IRecorder _currentRecorder;
        private readonly IOverlayDrawer _overlayDrawer;
        private ScoreScreenViewModel _currentMatch;

        public MatchRecorder(IOverlayDrawer overDrawer)
        {
            //_recorder = recorder;
            _overlayDrawer = overDrawer;

            //_recorder.NewFrame += RecorderOnNewFrame;
        }

        private void RecorderOnNewFrame(object sender, FrameGeneratedEventArgs e)
        {
            _overlayDrawer?.DrawOverlay(e.Frame, _currentMatch);
        }

        public void DeleteRecording(string storagePath, int matchNumber, Guid? tournamentId)
        {
            if (string.IsNullOrEmpty(storagePath)) return;

            if (_currentRecorder != null && _currentRecorder.IsRecording) StopRecording();

            string dirPath = GetFullStoragePath(tournamentId, storagePath);

            int partNumber = 1;

            string fileName = Path.Combine(dirPath, matchNumber + "_" + partNumber + DEFAULT_EXTENSION);

            while (File.Exists(fileName))
            {
                try
                {
                    File.Delete(fileName);
                }
                catch (Exception)
                {
                    // ignored
                }

                partNumber++;
                fileName = Path.Combine(dirPath, matchNumber + "_" + partNumber + DEFAULT_EXTENSION);
            }
        }

        public void StartRecording(string storagePath, RecorderConfiguration config, ScoreScreenViewModel match, Guid? tournamentId)
        {
            if (config == null || match == null) return;

            if (_currentRecorder != null && _currentRecorder.IsRecording)
            {
                StopRecording();
            }

            _currentMatch = match;
            
            var fileName = GetAvailableFileName(storagePath, match, tournamentId);

            _currentRecorder = FfmpegCamRecorder.StartRecording(fileName, config, RecorderOnNewFrame);
        }

        public void StopRecording()
        {
            _currentRecorder?.StopRecording();
        }

        public IEnumerable<string> GetMatchRecordings(string storagePath, WrestlingMatch match, Guid? tournamentId)
        {
            var result = new List<string>();

            var dirPath = GetFullStoragePath(tournamentId, storagePath);

            int partNumber = 1;
            var fileName = Path.Combine(dirPath, match.MatchNumber + "_" + partNumber + DEFAULT_EXTENSION);
            while (File.Exists(fileName))
            {
                result.Add(fileName);

                partNumber++;
                fileName = Path.Combine(dirPath, match.MatchNumber + "_" + partNumber + DEFAULT_EXTENSION);
            }

            return result;
        }

        public static string GetFullStoragePath(Guid? tournamentId, string baseStoragePath)
        {
            string dirPath = baseStoragePath;

            if (tournamentId.HasValue)
            {
                dirPath = Path.Combine(dirPath, tournamentId.Value.ToString());
            }
            else
            {
                dirPath = Path.Combine(dirPath, DateTime.Now.ToString("yyyyMMdd"));
            }

            return dirPath;
        }

        private string GetAvailableFileName(string storagePath, ScoreScreenViewModel match, Guid? tournamentId)
        {
            string dirPath = GetFullStoragePath(tournamentId, storagePath);

            if (!Directory.Exists(dirPath))
            {
                Directory.CreateDirectory(dirPath);
            }

            int partNumber = 1;
            var fileName = Path.Combine(dirPath, match.MatchFullNumber + "_" + partNumber + DEFAULT_EXTENSION);
            while (File.Exists(fileName))
            {
                partNumber++;
                fileName = Path.Combine(dirPath, match.MatchFullNumber + "_" + partNumber + DEFAULT_EXTENSION);
            }
            return fileName;
        }
    }
}