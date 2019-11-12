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

        bool IMatchRecorder.IsRecording => _currentRecorder != null ? _currentRecorder.IsRecording : false; 

        private void RecorderOnNewFrame(object sender, FrameGeneratedEventArgs e)
        {
            _overlayDrawer?.DrawOverlay(e.Frame, e.Time, _currentMatch);
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

            // LOGO
            _currentMatch.LogoImage = new System.Drawing.Bitmap("Images\\RosbosLogo.png");
            _currentMatch.LogoRectangle = new System.Drawing.RectangleF(50, 50, 100, 100);
            _currentMatch.LogoPosition = Model.LogoPositionEnum.RIGHT_BOTTOM;
            // LOGO

            var fileName = GetAvailableFileName(storagePath, match, tournamentId);

            _currentRecorder = FfmpegCamRecorder.StartRecording(
                fileName, 
                config, 
                RecorderOnNewFrame, 
                match.MaxRoundSecond * 1000); // we need ms
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
            string dirPath = Path.Combine(baseStoragePath, GetTournamentName(tournamentId));
            return dirPath;
        }

        public static string GetTournamentName(Guid? tournamentId)
        {
            if (tournamentId.HasValue)
            {
                return tournamentId.Value.ToString();
            }
            return  DateTime.Now.ToString("yyyyMMdd");
        }

        private string GetAvailableFileName(string storagePath, ScoreScreenViewModel match, Guid? tournamentId)
        {
            string dirPath = storagePath;// GetFullStoragePath(tournamentId, storagePath);

            if (!Directory.Exists(dirPath))
            {
                Directory.CreateDirectory(dirPath);
            }

            int partNumber = 1;
            var fileName = Path.Combine(dirPath, GetTournamentName(tournamentId) + "_" + match.MatchFullNumber + "_" + partNumber + DEFAULT_EXTENSION);
            while (File.Exists(fileName))
            {
                partNumber++;
                fileName = Path.Combine(dirPath, match.MatchFullNumber + "_" + partNumber + DEFAULT_EXTENSION);
            }
            return fileName;
        }

        public void SetTimerOffset(int t)
        {
            _currentRecorder?.SetTimerOffset(t);
        }

        public void SetMainSecond(int t)
        {
            _currentRecorder?.SetMainSecond(t);
        }

        public void CreateOverlay(bool flag)
        {
            _currentRecorder?.CreateOverlay(flag);
        }
    }
}