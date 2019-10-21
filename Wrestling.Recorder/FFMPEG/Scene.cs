using System;
using System.Collections.Generic;

namespace Wrestling.Recorder.FFMPEG
{
    [Serializable()]
    public class Scene
    {
        public static string ImageOverExt = ".tiff";
        public static System.Drawing.Imaging.ImageFormat ImageOverFmt = System.Drawing.Imaging.ImageFormat.Tiff;

        public Scene()
        {
            PrefixName = "out";
            ExtName = ".ts";
        }

        public static TimeSpan GetTimeProcessed(List<Scene> scenes)
        {
            double offset = 0;
            foreach (Scene sc in scenes)
                offset += sc.Len;
            return TimeSpan.FromMilliseconds(offset);
        }

        public int Len { get; set; }
        public int Step { get; set; }
        public int Group { get; set; }
        public int Index { get; set; }
        public long Size { get; set; }
        public float Speed { get; set; }
        public bool IsLast { get; set; }

        public String PrefixName { get; set; }
        public String ExtName { get; set; }

        public String NameOver
        {
            get
            {
                return $"over{Index.ToString("000000")}{ExtName}";
            }
        }

        public String NameCode
        {
            get
            {
                return $"out{Index.ToString("000000")}{ExtName}";
            }
        }

        public String NameImage
        {
            get
            {
                return $"over{Index.ToString("000000")}{ImageOverExt}";
            }
        }

        private List<String> _nameImages = null;

        public List<String> NameImages
        {
            get
            {
                if (_nameImages == null)
                {
                    _nameImages = new List<string>();
                    for (int i = 0; i < 10; i++)
                    {
                        _nameImages.Add($"over{(Index * 10 + i).ToString("000000")}{ImageOverExt}");
                    }
                }
                return _nameImages;
            }
        }
    }
}
