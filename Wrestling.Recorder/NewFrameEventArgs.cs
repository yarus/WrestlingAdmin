using System;
using System.Drawing;

namespace Wrestling.Recorder
{
    public class FrameGeneratedEventArgs : EventArgs
    {
        public Bitmap Frame { get; set; }
        public long Time { get; set; }
        public int Index { get; set; }

        public FrameGeneratedEventArgs(Bitmap frame, long time, int index)
        {
            Frame = frame;
            Time = time;
            Index = index;
        }

        public String FileName
        {
            get
            {
                return $"over{Index.ToString("000000")}.png";
            }
        }
    }
}