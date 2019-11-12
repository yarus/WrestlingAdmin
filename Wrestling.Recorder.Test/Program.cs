using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wrestling.Recorder.Test
{
    class Program
    {
        public static string TEST_FILE_NAME = "test.avi";
        public static string VIDEO_CODEC_PRESET = "veryfast";

        static void Main(string[] args)
        {
            var videoDevices = RecorderDevicesProvider.VideoDevices;

            Console.WriteLine("Select Video Device");
            for (int i = 0; i < videoDevices.Count; i++)
            {
                Console.WriteLine($"{i}: {videoDevices[i].Name}");
            }

            var deviceIndex = Convert.ToInt32(Console.ReadLine());
            
            Console.WriteLine("Select Resolution for Video Device");

            var resolutions = videoDevices[deviceIndex].Resolutions;
            for (int i = 0; i < resolutions.Count; i++)
            {
                Console.WriteLine($"{i}: {resolutions[i].Width} x {resolutions[i].Height} ({resolutions[i].AverageFrameRate} FPS)");
            }

            var resIndex = Convert.ToInt32(Console.ReadLine());

            Console.WriteLine("Select Audio Device");
            var audioDevices = RecorderDevicesProvider.AudioDevices;
            for (int i = 0; i < audioDevices.Count; i++)
            {
                Console.WriteLine($"{i}: {audioDevices[i].Name}");
            }
            Console.WriteLine($"{audioDevices.Count}: No audio device");

            var audioIndex = Convert.ToInt32(Console.ReadLine());

            var recorderConfiguration = new RecorderConfiguration
            {
                VideoDeviceID = videoDevices[deviceIndex].ID,
                VideoFrameRate = resolutions[resIndex].AverageFrameRate,
                VideoHeight = resolutions[resIndex].Height,
                VideoWidth = resolutions[resIndex].Width,
                Preset = VIDEO_CODEC_PRESET
            };

            if (audioIndex != audioDevices.Count)
            {
                recorderConfiguration.AudioDeviceID = audioDevices[audioIndex].ID;
            }

            var recorder = FfmpegCamRecorder.StartRecording(TEST_FILE_NAME, recorderConfiguration, NewFrame, 90);

            Console.WriteLine("Press Enter to start recording (to complete recording press Enter again)...");

            while (Console.ReadKey().Key != ConsoleKey.Enter)
            {
            }

            recorder.StopRecording();

            recorder = FfmpegCamRecorder.StartRecording(TEST_FILE_NAME, recorderConfiguration, NewFrame, 90);

            while (Console.ReadKey().Key != ConsoleKey.Enter)
            {
            }


            recorder.StopRecording();
        }

        private static void NewFrame(object sender, FrameGeneratedEventArgs e)
        {
            Console.WriteLine((e.Time / 1000.0).ToString("0.000"));
        }
    }
}