using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wrestling.Recorder.FFMPEG.Exceptions
{
    public class FfmpegProcessFailureException : FfmpegCodingException
    {
        private String Text { get; set; }

        public FfmpegProcessFailureException(String text)
        {
            Text = text;
        }

        public override string Message
        {
            get
            {
                return Text;
            }
        }
    }
}