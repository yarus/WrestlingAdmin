using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wrestling.Recorder.FFMPEG
{
    public class MediaStream
    {
        public int Index { get; set; }
        public int SubIndex { get; set; }
        public String Lang { get; set; }
        public StreamTypeEnum StreamType { get; set; }
        public String Name { get; set; }
        public String AlterName { get; set; }
        public String Code { get; set; }
        public int Bitrate { get; set; }
        public String Fps { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        public int Order
        {
            get
            {
                if (StreamType == StreamTypeEnum.Audio)
                    return 1;

                if (StreamType == StreamTypeEnum.Subtitle)
                    return 2;

                return 0;
            }
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
