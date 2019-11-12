using System;
using System.Drawing;

namespace Wrestling.Recorder
{
    public class FrameGeneratedEventArgs : EventArgs
    {
        public Bitmap Frame { get; set; }
        public long Time { get; set; }
        public int Index { get; set; }
        public bool IsDrawTimer { get; set; }

        public FrameGeneratedEventArgs(Bitmap frame, long time, int index, bool isDrawTimer)
        {
            Frame = frame;
            Time = time;
            Index = index;
            IsDrawTimer = isDrawTimer;
        }

        public string FileName
        {
            get
            {
                return $"over{Index.ToString("000000")}{FFMPEG.Scene.ImageOverExt}";
            }
        }
    }
}