using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using Wrestling.Recorder.FFMPEG.Exceptions;

namespace Wrestling.Recorder.FFMPEG
{
    public class Ffprobe
    {
        public int Duration { get; set; }
        public float SAR { get; set; }
        public float PAR { get; set; }
        public float DAR { get; set; }

        private Dictionary<String, String> general = new Dictionary<string, string>();
        private List<Dictionary<String, String>> dict = new List<Dictionary<string, string>>();
        public List<MediaStream> Streams { get; set; } = new List<MediaStream>();
        public String FileName { get; set; }

        public List<String> strings = null;
        public bool ioerror = false;

        public Ffprobe()
        {
            Process ffdev = Ffmpeg.CreateFfmpegProcess("-list_devices true -f dshow -i dummy");
            ffdev.ErrorDataReceived += HandleFfmpegDetectDevicesErrorDataReceived;
            ffdev.Start();
            ffdev.BeginErrorReadLine();
            ffdev.WaitForExit();
        }

        private int canGetDev = 0;
        private int canGetDevIndex = 0;
        private void HandleFfmpegDetectDevicesErrorDataReceived(object sender, System.Diagnostics.DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                String line = e.Data.ToString();
                if (line.Contains("DirectShow video devices"))
                {
                    canGetDev = 1;
                    return;
                }

                if (line.Contains("DirectShow audio devices"))
                {
                    canGetDev = 2;
                    return;
                }

                if (canGetDev == 1)
                {
                    String[] ll = line.Split(new String[] { " \"" }, StringSplitOptions.None);
                    if (ll.Length == 2)
                    {
                        Streams.Add(new MediaStream
                        {
                            StreamType = StreamTypeEnum.Video,
                            Name = ll[1].Replace("\"", ""),
                            Index = canGetDevIndex++,
                        });
                    }
                }

                if (canGetDev == 2)
                {
                    String[] ll = line.Split(new String[] { " \"" }, StringSplitOptions.None);
                    if (ll.Length == 2)
                    {
                        Streams.Add(new MediaStream
                        {
                            StreamType = StreamTypeEnum.Audio,
                            Name = GetName(ll[1]),
                            Index = canGetDevIndex++,
                        });
                    }
                }
            }
        }

        private String GetName(String line)
        {
            line = "\"" + line;
            String res = String.Empty;
            bool b = false;
            foreach (char s in line)
            {
                if (b && !s.Equals('"'))
                    res += s;
                if (s.Equals('"'))
                    b = !b;
            }

            return res;
        }

        public Ffprobe(String fileName)
        {
            FileName = fileName;

            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");

            /*Process proc = new Process();
            proc.StartInfo.CreateNoWindow = true;
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.ErrorDialog = false;
            proc.StartInfo.RedirectStandardOutput = true;
            proc.StartInfo.RedirectStandardInput = true;
            proc.StartInfo.RedirectStandardError = true;*/

            Uri u = new Uri(fileName);
            if (u.IsFile && !System.IO.File.Exists(fileName))
                throw new System.IO.FileNotFoundException();

            StringBuilder sb = new StringBuilder();

            if (fileName.Contains("rtmp://") || fileName.Contains("tcp://") || fileName.Contains("http://") || fileName.Contains("https://"))
            {
                sb.Append(" -i \"" + fileName + "\"");
            }
            else
            if (fileName.Contains(".playlist"))
            {
                sb.Append(" -f concat -safe 0 -i \"" + fileName + "\"");
            }
            else
            if (!System.IO.File.Exists(fileName))
            {
                sb.Append(" -f vfwcap -i " + fileName);
            }
            else
            {
                sb.Append(" -i \"" + fileName + "\"");
            }

            Process proc = Ffmpeg.CreateFfmpegProcess(sb.ToString());

            proc.OutputDataReceived += FfmpegOutpuOutput;
            proc.ErrorDataReceived += FfmpegErrorOutput;

            proc.Start();
            proc.BeginErrorReadLine();
            proc.BeginOutputReadLine();

            proc.WaitForExit();

            if (ioerror)
                throw new IOVideoDeviceException { FileName = FileName };

            if (strings == null || strings.Count == 0)
                throw new FfprobeDurationException { FileName = FileName };

            String sss = strings.First(o => o.Contains("Duration"));
            sss = sss.Replace("  Duration: ", "").Split(new String[] { ", " }, StringSplitOptions.None)[0];

            if (!sss.Equals("N/A"))
            {
                String[] tms = sss.Split(new String[] { ":" }, StringSplitOptions.None);
                int curr = Convert.ToInt32((Convert.ToInt32(tms[0]) * 3600 + Convert.ToInt32(tms[1]) * 60 + Convert.ToDouble(tms[2])) * 1000.0);

                Duration = curr;
            }

            foreach (String s in strings.Where(o => o.Contains(" Stream #0:")))
            {
                MediaStream stream = CreateStream(s);
                if (stream == null)
                    continue;

                stream.SubIndex = Streams.Count(o => o.StreamType == stream.StreamType);

                if (stream.StreamType == StreamTypeEnum.Video)
                {
                    String[] ss = s.Split(new String[] { ": Video: " }, StringSplitOptions.None);
                    bool block = false;
                    String line = String.Empty;
                    foreach (char ch in ss[1])
                    {
                        if (ch == '(')
                        {
                            block = true;
                            continue;
                        }
                        if (ch == ')')
                        {
                            block = false;
                            continue;
                        }
                        if (!block)
                            line += ch;
                    }

                    String[] par = line.Split(new String[] { ", " }, StringSplitOptions.None);

                    //Размеры
                    String size_s = FindByMask(par, "x");
                    if (!String.IsNullOrEmpty(size_s))
                    {
                        if (par[2].Contains(" ["))
                            size_s = size_s.Split(new String[] { " [" }, StringSplitOptions.None)[0];

                        String[] sz = size_s.Split('x');

                        if (sz.Length == 2)
                        {
                            int w = 0;
                            if (Int32.TryParse(sz[0], out w))
                                stream.Width = w;

                            int h = 0;
                            if (Int32.TryParse(sz[1], out h))
                                stream.Height = h;
                        }
                    }

                    //фпс
                    String fps_s = FindByMask(par, " fps");
                    if (!String.IsNullOrEmpty(fps_s))
                    {
                        stream.Fps = fps_s.Replace(" fps", "");

                    }

                    if (fileName.Contains("rtmp://") || fileName.Contains("tcp://") || fileName.Contains(".m3u8"))
                    {
                        PAR = 1;
                    }
                    else
                    {
                        PAR = 1;
                    }
                }

                if (stream != null)
                    Streams.Add(stream);
            }
        }

        private String FindByMask(String[] list, String mask)
        {
            foreach (String s in list)
                if (s.Contains(mask))
                    return s;

            return String.Empty;
        }

        private MediaStream CreateStream(String s)
        {
            MediaStream stream = null;
            if (s.Contains(": Video: "))
                stream = new MediaStream() { StreamType = StreamTypeEnum.Video };
            if (s.Contains(": Audio: "))
                stream = new MediaStream() { StreamType = StreamTypeEnum.Audio };
            if (s.Contains(": Subtitle: "))
                stream = new MediaStream() { StreamType = StreamTypeEnum.Subtitle };

            if (stream == null)
                return null;

            stream.Width = 1920;
            stream.Height = 1080;

            String[] ss = s.Split(new String[] { ": Video: ", ": Audio: ", ": Subtitle: " }, StringSplitOptions.None);
            String ind = ss[0].Replace("Stream #0:", "").Trim();
            String ind2 = "";
            foreach (Char ch in ind)
            {
                if (!"0123456789".Contains(ch))
                    break;
                ind2 += ch;
            }

            bool chr = false;
            String streamName = "";
            foreach (Char ch in ind)
            {
                if ("]".Contains(ch) || ")".Contains(ch))
                    break;

                if ("[".Contains(ch) || "(".Contains(ch))
                {
                    chr = true;
                    streamName = "";
                    continue;
                }

                if (chr)
                    streamName += ch;
            }

            if (ss.Length > 1)
            {
                if (String.IsNullOrEmpty(streamName))
                {
                    streamName = ss[1];
                }

                String[] adv = ss[1].Split(new String[] { ", " }, StringSplitOptions.None);
                foreach (String advi in adv)
                {
                    if (advi.Contains(" kb/s"))
                    {
                        //max. 9100 kb/s
                        String brs = advi.Replace(" kb/s", "");
                        brs = GetInteger(brs);
                        int br = 0;
                        if (Int32.TryParse(brs, out br))
                            stream.Bitrate = br;
                    }
                }
            }

            stream.Index = Int32.Parse(ind2);
            stream.Name = s.Replace("Stream", "").Trim();
            stream.Code = streamName;

            return stream;
        }

        private String GetInteger(String s)
        {
            String ind2 = "";
            foreach (Char ch in s)
            {
                if (!"0123456789".Contains(ch))
                    continue;
                ind2 += ch;
            }

            return ind2;
        }

        private void FfmpegErrorOutput(object sender, System.Diagnostics.DataReceivedEventArgs e)
        {
            //if (e.Data is String)
            //Log.Write(String.Format("FFPROBE => {0}", e.Data));

            if (e.Data != null && e.Data.Contains("Input/output error"))
                ioerror = true;

            if (e.Data != null && (e.Data.Contains("Input #0,") || strings != null))
            {
                if (strings == null)
                    strings = new List<string>();

                strings.Add(e.Data.ToString());
            }
        }

        private void FfmpegOutpuOutput(object sender, System.Diagnostics.DataReceivedEventArgs e)
        {
            //if (e.Data is String)
            //Log.Write(String.Format("FFPROBE => {0}", e.Data));

            if (e.Data != null && e.Data.Contains("Input/output error"))
                ioerror = true;

            if (e.Data != null && (e.Data.Contains("Input #0,") || strings != null))
            {
                if (strings == null)
                    strings = new List<string>();

                strings.Add(e.Data.ToString());
            }
        }
    }
}
