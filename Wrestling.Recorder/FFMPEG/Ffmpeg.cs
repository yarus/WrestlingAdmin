using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace Wrestling.Recorder.FFMPEG
{
    public class Ffmpeg
    {
        public static String FfmpegExe_x64 { get; set; } = @".\ffmpeg_x64.exe";
        public static String FfmpegExe_x64_long { get; set; } = @".\ffmpeg\bin\ffmpeg_x64.exe";

        /*ffmpeg\bin\ffmpeg_x64 -y -f dshow -s 640x480 -re -framerate 30 
         * -i video="@device_pnp_\\?\usb#vid_04f2&pid_b56b&mi_00#6&25ee911b&0&0000#{65e8773d-8f56-11d0-a3b9-00a0c9223196}\global"
         * :audio="@device_cm_{33D9A762-90C8-11D0-BD43-00A0C911CE86}\wave_{81FA5AA0-4E3D-4C09-80F9-12C4F83CC501}" 
         * -c:v mpeg4 -r 30 -b:v 3000k -acodec aac -ar 22050 -ab 96k -f mp4 -movflags +faststart 123.mp4*/

        public static Process CreateRecordProcess(
            String video_device,
            String audio_device,
            int source_width,
            int source_height,
            float source_fps,
            float result_fps,
            String temp_dir,
            String result_vbitrate = "8000K",
            int result_vquality = 1,
            String result_abitrate = "96K",
            String result_freq = "22050",
            String vcodec = "mpeg4",
            String acodec = "libfdk_aac",
            String preset = "ultrafast")
        {
            StringBuilder sb = new StringBuilder();

            bool mute = String.IsNullOrEmpty(audio_device);

            sb.AppendFormat(" -y -rtbufsize 2004000k -f dshow -s {0}x{1} -framerate {2}",
                source_width,
                source_height,
                source_fps.ToString(CultureInfo.InvariantCulture));

            sb.AppendFormat(" -i video=\"{0}\"",
                video_device);

            if (!mute)
            {
                sb.AppendFormat(":audio=\"{0}\"",
                    audio_device);
            }

            //-keyint_min {3} -sc_threshold 0 
            sb.AppendFormat(" -c:v {0} -r {1} -q:v {2} -pix_fmt yuv420p  -b:v {3} -minrate {3} -maxrate {3} -bufsize {3}",
                vcodec,
                result_fps,
                result_vquality,
                result_vbitrate,
                preset);

            if (!mute)
            {
                sb.AppendFormat(" -acodec {0} -ar {1} -ab {2}",
                    acodec,
                    result_freq,
                    result_abitrate);
            }
            else
            {
                sb.Append(" -an");
            }

            sb.AppendFormat(" -f segment -segment_time 10 -segment_start_number 0 -segment_format mpegts -flags -global_header");
            sb.AppendFormat(" -segment_list \"{0}\\out.m3u8\" \"{0}\\out%06d.ts\"",
                temp_dir);

            return CreateFfmpegProcess(sb.ToString());
        }

        public static Process AttachOverlay(
            String video_filename,
            String path_to_overlay,
            String result_filename,
            float source_fps,
            double offset_segment,
            bool mute,
            String result_vbitrate = "8000K",
            int result_vquality = 1,
            String vcodec = "mpeg4",
            String acodec = "libfdk_aac",
            String result_abitrate = "96K",
            String result_freq = "22050")
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendFormat(" -y -i {0}",
                video_filename);

            sb.AppendFormat(" -r 1 -f image2 -i \"{0}\\over%6d{1}\"",
                path_to_overlay, Scene.ImageOverExt);

            sb.AppendFormat(" -filter_complex \"[0:0][1:0]overlay=0:0[m]\"");

            sb.AppendFormat(" -c:v {0} -q:v {2} -r {1} -b:v {3}",
                vcodec,
                source_fps,
                result_vquality,
                result_vbitrate);

            if (!mute)
                sb.AppendFormat(" -acodec {0} -ar {1} -ab {2}",
                    acodec,
                    result_freq,
                    result_abitrate);
            else
                sb.Append(" -an");

            sb.AppendFormat(" -output_ts_offset {0} -mpegts_copyts 1",
                (offset_segment / 1000.0).ToString("0.000000").Replace(",", "."));

            sb.AppendFormat(" -map \"[m\"]");

            if (!mute)
                sb.Append(" -map 0:a");

            sb.AppendFormat(" -f mpegts \"{0}\"",
                result_filename);

            return CreateFfmpegProcess(sb.ToString());
        }

        public static Process AttachImage(
            String video_filename,
            String path_to_overlay,
            String result_filename,
            float source_fps,
            double offset_segment,
            bool mute,
            String result_vbitrate = "8000K",
            int result_vquality = 1,
            String vcodec = "mpeg4")
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendFormat(" -y -i {0}",
                video_filename);

            sb.AppendFormat(" -f image2 -i \"{0}\"",
                path_to_overlay);

            sb.AppendFormat(" -filter_complex \"[0:0][1:0]overlay=0:0[m]\"");

            /*sb.AppendFormat(" -c:v {0} -r {1} -b:v 3000k",
                vcodec,
                source_fps);*/

            sb.AppendFormat(" -c:v {0} -r {1} -q:v {2} -pix_fmt yuv420p  -b:v {3} -minrate {3} -maxrate {3} -bufsize {3}",
                vcodec,
                source_fps,
                result_vquality,
                result_vbitrate);

            if (!mute)
                sb.Append(" -c:a copy");
            else
                sb.Append(" -an");

            sb.AppendFormat(" -output_ts_offset {0} -mpegts_copyts 1",
                (offset_segment / 1000.0).ToString("0.000000").Replace(",", "."));

            sb.AppendFormat(" -map \"[m\"]");

            if (!mute)
                sb.Append(" -map 0:a");

            sb.AppendFormat(" -f mpegts \"{0}\"",
                result_filename);

            return CreateFfmpegProcess(sb.ToString());
        }

        public static Process Joint(
            String play_list,
            List<String> ofList,
            String outFile,
            bool mute,
            int start = 0,
            int len = 0,
            String format = "mp4",
            String vcodec = "copy",
            String acodec = "copy")
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendFormat(" -y -f concat -safe 0 -i \"{0}\"", play_list);

            if (start > 0)
                sb.AppendFormat(" -ss {0}", ((float)start / 1000f).ToString().Replace(",", "."));

            if (len > 0)
                sb.AppendFormat(" -t {0}", ((float)len / 1000f).ToString().Replace(",", "."));

            sb.AppendFormat(" -vcodec {0}", vcodec);

            if (!mute)
                sb.AppendFormat(" -acodec {0}", acodec);
            else
                sb.Append(" -an");

            if (format.ToUpper().Contains("MP4"))
                sb.AppendFormat(" -movflags +faststart");

            if (!String.IsNullOrEmpty(format))
                sb.AppendFormat(" -f {0}", format);

            sb.AppendFormat(" \"{0}\"", outFile);

            return CreateFfmpegProcess(sb.ToString());
        }

        public static Process CreateRecordProcessFile(
            String video_device,
            String audio_device,
            int source_width,
            int source_height,
            float source_fps,
            String result_filename,
            String vcodec = "mpeg4",
            String acodec = "aac")
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendFormat(" -y -f dshow -s {0}x{1} -re -framerate {2}",
                source_width,
                source_height,
                source_fps.ToString(CultureInfo.InvariantCulture));

            sb.AppendFormat(" -i video=\"{0}\":audio=\"{1}\"",
                video_device,
                audio_device);

            sb.AppendFormat(" -c:v {0} -r {1} -b:v 3000k -acodec {2} -ar 22050 -ab 96k -f mpegts -flags -global_header \"{3}\"",
                vcodec,
                source_fps,
                acodec,
                result_filename);

            return CreateFfmpegProcess(sb.ToString());
        }

        /*-y -i "123.mp4" -r 1 -f image2 -i "img%%6d.png" -t 120 
         * -filter_complex "[0:0][1:0]overlay=0:0[m]"  -c:v mpeg4 -r 30 -b:v 4000k -acodec aac -ar 22050 -ab 96k 
         * -map "[m"] -map 0:a -f mp4 -movflags +faststart -pix_fmt yuv420p 333.mp4*/

        private static int random = 0;
        public static String GetNextRandom()
        {
            return String.Format("{0:D9}", random++);
        }

        public static Process CreateFfmpegProcess(String args)
        {
            Process proc = new Process();
            proc.StartInfo.CreateNoWindow = true;
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.ErrorDialog = false;
            proc.StartInfo.RedirectStandardOutput = true;
            proc.StartInfo.RedirectStandardInput = true;
            proc.StartInfo.RedirectStandardError = true;
            proc.EnableRaisingEvents = true;

            //args = "-y -f dshow -s 640x480 -re -framerate 30 -i video=\"@device_pnp_\\\\?\\usb#vid_04f2&pid_b56b&mi_00#6&25ee911b&0&0000#{65e8773d-8f56-11d0-a3b9-00a0c9223196}\\global\":audio=\"@device_cm_{33D9A762-90C8-11D0-BD43-00A0C911CE86}\\wave_{81FA5AA0-4E3D-4C09-80F9-12C4F83CC501}\" -c:v mpeg4 -r 30 -b:v 3000k -acodec aac -ar 22050 -ab 96k -f mp4 -movflags +faststart \"c:\\Data\\Projects\\cs2\\Wrestling\\release\\temp\\record.mp4\"";
            proc.StartInfo.Arguments = args;

            if (File.Exists(FfmpegExe_x64))
            {
                proc.StartInfo.FileName = FfmpegExe_x64;
                Console.WriteLine($"{FfmpegExe_x64} {args}");
            }
            else
            {
                proc.StartInfo.FileName = FfmpegExe_x64_long;
                Console.WriteLine($"{FfmpegExe_x64} {args}");
            }

            return proc;
        }
    }
}