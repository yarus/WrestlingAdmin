using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wrestling.Recorder.FFMPEG.Exceptions
{
    public class FfmpegCodingException : Exception
    {
        public static void ParseAndThrowException(String msg)
        {
            //[h264_qsv @ 0000026dfd7be240] Error during encoding: device failed (-17) 
            if (msg.Contains("h264_qsv") && msg.Contains("Error during encoding: device failed"))
                throw new QSVFfmpegCodingException();
        }
    }
}