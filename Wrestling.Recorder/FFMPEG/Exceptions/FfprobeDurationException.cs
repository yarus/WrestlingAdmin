using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wrestling.Recorder.FFMPEG.Exceptions
{
    public class FfprobeDurationException : Exception
    {
        public String FileName { get; set; }

        public override string Message
        {
            get
            {
                return "Невозможно определить параметры " + FileName;
            }
        }
    }
}