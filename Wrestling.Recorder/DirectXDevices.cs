using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wrestling.Recorder.FFMPEG;

namespace Wrestling.Recorder
{
    public class DirectXDevices
    {
        public static List<FfprobeStream> List { get; set; } = new List<FfprobeStream>();

        public static void Refresh()
        {
            Ffprobe ff = new Ffprobe();
            List = ff.Streams;
        }
    }
}
