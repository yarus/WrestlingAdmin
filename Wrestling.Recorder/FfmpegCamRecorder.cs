using EmguFFmpeg;
using EmguFFmpeg.EmguCV;
using FFmpeg.AutoGen;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Uniso.Helpers.Windows;
using Uniso.Threading;
using Wrestling.Recorder.FFMPEG;

namespace Wrestling.Recorder
{
    public class FfmpegCamRecorder : IDisposable, IRecorder
    {
        private const int DEFAULT_FRAME_RATE = 30;
        private const int DEFAULT_HEIGHT = 480;
        private const int DEFAULT_WIDTH = 640;
        private const string DEFAULT_PRESET = "veryfast";
        private const int DEFAULT_SAMPLE_RATE = 44100;

        public event EventHandler<FrameGeneratedEventArgs> NewFrame;
        public event EventHandler<Exception> RecordException;
        public event EventHandler<Exception> OverlayException;
        public event EventHandler<Exception> ConcatException;
        public event EventHandler<string> RecordProcess;
        public event EventHandler<double> OverlayProcess;
        public event EventHandler RecordFinishing;
        public event EventHandler RecordCompleted;
        public event EventHandler<string> ConcatCompleted;
        public event EventHandler<BitmapSource> FrameSourceReady;
        public static event EventHandler CaptureStart;
        public static event EventHandler CaptureStop;

        #region Fields

        private Process proc_enc = null;
        private RecorderConfiguration _configuration;
        private string _fileName;
        private CancellationTokenSource cts = new CancellationTokenSource();

        private static Regex pattern_time =
            new Regex(@"\ time=(?<val>.*?)\ bitrate=",
                RegexOptions.Compiled |
                RegexOptions.Singleline);

        //drop = 0 speed = 1.04x
        private static Regex pattern_rate =
            new Regex(@"\ speed=(?<val>.*?)\\?x",
                RegexOptions.Compiled |
                RegexOptions.Singleline);

        private static Regex pattern_guid =
            new Regex(@"\{(?<val>.*?)\}",
                RegexOptions.Compiled |
                RegexOptions.Singleline);

        private List<Scene> scenes = new List<Scene>();
        private double offset_segment = 0.0;
        //private WaveEncoder _encoder;

        private readonly SyncObject _syncObj = new SyncObject();

        #endregion

        public DateTime RecordingStartTime { get; private set; }
        public bool IsRecording { get; private set; }
        public int OverlayPeriod { get; set; } = 1000;
        public int OverlayPostprocessTime { get; set; } = 10000;

        private long _timeTimerOffset = 0;
        private long _timeCurrent = 0L;
        private bool _createOverlay = false;
        private long _timeOffset = 0L;
        private long _halfTime = 180000L;
        private long _currentTimer = 0;

        void IRecorder.SetTimerOffset(int t)
        {
            _timeTimerOffset = _timeCurrent - t;
        }

        public void CreateOverlay(bool flag)
        {
            _createOverlay = flag;
        }

        public void SetMaxSeconds(int seconds)
        {
            _halfTime = seconds * 1000;
        }

        public void SetMainSecond(int t)
        {
            _currentTimer = t * 1000;
        }

        public FfmpegCamRecorder(string fileName, RecorderConfiguration configuration, long halfTime)
        {
            if (IsRecording || string.IsNullOrEmpty(fileName))
            {
                return;
            }

            _fileName = fileName;
            _configuration = configuration;
            _halfTime = halfTime;
            _currentTimer = 0;

            _timer = new DispatcherTimer();
            _timer.Tick += _timer_Tick;
            _timer.Interval = TimeSpan.FromMilliseconds(40);
        }

        [DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);

        private void _timer_Tick(object sender, EventArgs e)
        {
            if (_framesProcessStage != 1)
                return;

            try
            {
                if (_frameSource != null && FrameSourceReady != null)
                {
                    FrameSourceReady?.Invoke(null, _frameSource.ToImageSource());
                }
            }
            finally
            {
                _framesProcessStage = 0;
            }
        }

        private static void DeleteBitmap(Bitmap bmp)
        {
            if (bmp != null)
            {
                var hBitmap = bmp.GetHbitmap();
                DeleteObject(hBitmap);
            }
        }

        public static FfmpegCamRecorder StartRecording(
            string fileName,
            RecorderConfiguration configuration,
            EventHandler<FrameGeneratedEventArgs> _newFrame,
            long halfTime,
            EventHandler<string> _recordProcess = null,
            EventHandler<double> _overlayProcess = null,
            EventHandler _recordFinishing = null,
            EventHandler _recordCompleted = null,
            EventHandler<string> _concatCompleted = null,
            EventHandler<Exception> _recordException = null,
            EventHandler<Exception> _overlayException = null,
            EventHandler<Exception> _concatException = null)
        {
            var r = new FfmpegCamRecorder(fileName, configuration, halfTime)
            {
                NewFrame = _newFrame
            };

            r.RecordProcess += _recordProcess;
            r.OverlayProcess += _overlayProcess;
            r.RecordFinishing += _recordFinishing;
            r.RecordCompleted += _recordCompleted;
            r.ConcatCompleted += _concatCompleted;
            r.RecordException += _recordException;
            r.OverlayException += _overlayException;
            r.ConcatException += _overlayException;

            r.StartRecording();
            return r;
        }

        public void StartRecording()
        {
            if (/*IsRecording || */string.IsNullOrEmpty(_fileName))
            {
                return;
            }

            DirectXDevices.Refresh();

            var video_name = _configuration.VideoDeviceID.ToUpper();
            var video_guid = "";

            var match = pattern_guid.Match(video_name);
            while (match.Success)
            {
                video_guid = match.Groups[1].Value.ToUpper();
                match = match.NextMatch();
            }

            if (string.IsNullOrEmpty(video_guid))
                throw new Exception("Could not find video GUID!");

            var video_stream = DirectXDevices.List.FirstOrDefault(o => o.Code.ToUpper().Contains(video_guid));

            FfprobeStream audio_stream = null;
            if (_configuration.AudioDeviceID != null)
            {
                var audio_guid = _configuration.AudioDeviceID.ToString().ToUpper();
                audio_stream = DirectXDevices.List.FirstOrDefault(o => o.Code.ToUpper().Contains(audio_guid));
                if (audio_stream == null)
                    audio_stream = DirectXDevices.List.FirstOrDefault(o => o.StreamType == StreamTypeEnum.Audio);
            }

            var dir = Path.GetDirectoryName(_fileName);
            var name = Path.GetFileNameWithoutExtension(_fileName);
            /*var tmp_dir = Path.Combine(dir, "temp", name);
            if (!Directory.Exists(tmp_dir))
            {
                Directory.CreateDirectory(tmp_dir);
            }
            else
            {
                ClearDir(tmp_dir);
            }*/

            cts = new CancellationTokenSource();
            Task.Factory.StartNew(() =>
            {
                TaskRecordVideoAndCreateOverlay(_fileName, video_stream, audio_stream, cts.Token);
            });
        }

        private void ParseM3U8(string dir, List<Scene> list)
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");

            var fileName = dir + @"\out.m3u8";
            var prefix = "out";

            if (!File.Exists(fileName))
                return;

            try
            {
                var res = new List<Scene>();
                res.AddRange(list);

                using (var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var sr = new StreamReader(fs))
                {
                    string line = null;
                    string extInf = null;
                    while ((line = sr.ReadLine()) != null)
                    {
                        if (string.IsNullOrEmpty(extInf) && line.Contains("#EXTINF:"))
                        {
                            extInf = line;
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(extInf) && line.Contains(prefix))
                            {
                                var numStr = line.Replace(prefix, "").Replace(".mp4", "").Replace(".ts", "");
                                var lenStr = extInf.Replace("#EXTINF:", "").Replace(",", "");
                                if (int.TryParse(numStr, out var num) && !res.Any(o => o.Index == num) && float.TryParse(lenStr, out var len))
                                {
                                    var sc = new Scene
                                    {
                                        Index = num,
                                        Len = Convert.ToInt32(len * 1000),
                                        ExtName = ".ts",
                                        PrefixName = prefix,
                                        Step = 0,
                                    };

                                    res.Add(sc);
                                }

                                extInf = null;
                            }
                        }
                    }

                    list.Clear();
                    list.AddRange(res.OrderBy(o => o.Index));
                }
            }
            catch
            { }
        }

        private bool CreateOverlay(
            string tmp_dir,
            List<Tuple<int, string>> overley_list,
            bool mute,
            bool record_is_over,
            CancellationToken token)
        {
            List<Scene> notProcList = null;
            lock (scenes)
            {
                ParseM3U8(tmp_dir, scenes);

                //Последний кусок
                if (record_is_over && scenes.Count > 0)
                {
                    foreach (var fnout in Directory.GetFiles(tmp_dir, "out*.ts"))
                    {
                        var fiout = new FileInfo(fnout);
                        var fn_index = fiout.Name.Replace("out", "").Replace(".ts", "");
                        if (int.TryParse(fn_index, out var f_index))
                        {
                            if (!scenes.Any(o => o.Index == f_index))
                            {
                                var ff = new Ffprobe(fiout.FullName);
                                scenes.Add(
                                    new Scene
                                    {
                                        Index = f_index,
                                        Len = ff.Duration,
                                        ExtName = ".ts",
                                        PrefixName = "out",
                                        Step = 0,
                                        IsLast = true,
                                    });
                            }
                        }
                    }
                }

                notProcList = scenes
                    .Where(o
                        => o.Step == 0
                        && (o.IsLast || o.NameImages.All(o1 => overley_list.Any(o2 => o2.Item2.Contains(o1)))))
                    .ToList();
            }

            foreach (var sc in notProcList)
            {
                if (token.IsCancellationRequested)
                    return true;

                var tmp_dir_over = Path.Combine(tmp_dir, "over");
                if (!Directory.Exists(tmp_dir_over))
                {
                    Directory.CreateDirectory(tmp_dir_over);
                }
                else
                {
                    ClearDir(tmp_dir_over);
                }

                try
                {
                    //Переносим в темп для овера
                    int index = 0;
                    foreach (var fn in sc.NameImages)
                    {
                        var src = Path.Combine(tmp_dir, fn);

                        if (!File.Exists(src))
                            continue;

                        var dest = Path.Combine(tmp_dir_over, $"over{index.ToString("000000")}{Scene.ImageOverExt}");
                        File.Move(src, dest);
                        index++;
                    }

                    var video_fn = Path.Combine(tmp_dir, sc.NameCode);
                    var result_fn = Path.Combine(tmp_dir, sc.NameOver);

                    var last_log = "";
                    var proc_a = Ffmpeg.AttachOverlay(
                        video_fn,
                        tmp_dir_over,
                        result_fn,
                        _configuration.VideoFrameRate.Value,
                        offset_segment,
                        mute,
                        _configuration.VBitrate,
                        _configuration.VQuality,
                        _configuration.VCodec,
                        _configuration.ACodec,
                        _configuration.ABitrate,
                        _configuration.AFrequency);

                    proc_a.ErrorDataReceived += (object sender, DataReceivedEventArgs e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            last_log = e.Data;
                        }
                    };

                    proc_a.Start();
                    proc_a.BeginErrorReadLine();

                    proc_a.WaitForExit();

                    if (proc_a.ExitCode != 0)
                    {
                        OverlayException?.Invoke(this, new Exception(last_log));
                        break;
                    }

                    offset_segment += sc.Len;

                    OverlayProcess?.Invoke(this, offset_segment);

                    Console.WriteLine($"-->Over {result_fn}");

                    File.Delete(video_fn);

                    sc.Step = 1;

                    //Чистим
                    ClearDir(tmp_dir_over);
                    Directory.Delete(tmp_dir_over);
                }
                finally
                {
                }
            }

            var ret = false;
            lock (scenes)
            {
                if (scenes.Count == 0)
                    ret = record_is_over;
                else
                    ret = scenes.All(o => o.Step == 1) && record_is_over;
            }

            return ret;
        }

        private static DispatcherTimer _timer;
        private static Bitmap _frameSource;
        private static int _framesProcessStage = 0; // 0 - можно записать, 1 - можно читать

        private void TaskRecordVideoAndCreateOverlay(
            string fileName,
            FfprobeStream video_stream,
            FfprobeStream audio_stream,
            CancellationToken token)
        {
            try
            {
                var formatInput = new InFormat("dshow"); ;
                var deviceVInput = $"video={video_stream.Code}";
                var deviceAInput = $"audio={audio_stream.Code}";

                var codecId = AVCodecID.AV_CODEC_ID_H264;//.AV_CODEC_ID_MPEG4;//.AV_CODEC_ID_MPEG1VIDEO;
                var bitrate = 24L * 1024L * 1024L;
                var opfmt = AVPixelFormat.AV_PIX_FMT_YUV420P;

                using (var writer = new MediaWriter(fileName, new OutFormat("mp4")))//, options))
                using (var reader0 = new MediaReader(deviceVInput, formatInput))
                using (var reader1 = new MediaReader(deviceAInput, formatInput))
                {
                    var videoStream = reader0.First(_ => _.Codec.Type == AVMediaType.AVMEDIA_TYPE_VIDEO);

                    // init filter source
                    int height = videoStream.Codec.AVCodecContext.height;
                    int width = videoStream.Codec.AVCodecContext.width;
                    int format = (int)videoStream.Codec.AVCodecContext.pix_fmt;
                    var time_base = videoStream.TimeBase;
                    var sample_aspect_ratio = videoStream.Codec.AVCodecContext.sample_aspect_ratio;
                    var r = videoStream.Stream.avg_frame_rate;
                    var fps = Convert.ToDouble(r.num) / Convert.ToDouble(r.den);
                    var frameDuration = 1000 / fps;

                    // add video stream
                    var streamV = MediaEncode.CreateVideoEncode(
                        codecId, 0,
                        //writer.Format,
                        width,
                        height,
                        (int)fps,
                        bitrate,
                        opfmt);

                    // init video frame format converter by dstcodec
                    var outStrmV = writer.AddStream(streamV);
                    var pixelConverter = new PixelConverter(outStrmV.Codec);

                    var audioStream = reader1.First(_ => _.Codec.Type == AVMediaType.AVMEDIA_TYPE_AUDIO);

                    // add audio stream
                    var streamA = MediaEncode.CreateAudioEncode(
                        writer.Format,
                        audioStream.Codec.AVCodecContext.channels,
                        audioStream.Codec.AVCodecContext.sample_rate);

                    writer.AddStream(streamA);

                    var outStrmA = writer[1];
                    var dstFrameA = AudioFrame.CreateFrameByCodec(outStrmA.Codec);
                    var converter = new SampleConverter(dstFrameA);

                    // init
                    writer.Initialize();

                    var capture = false;

                    var timer = new Stopwatch();
                    _timer.Start();

                    var filterGraph = new MediaFilterGraph();
                    filterGraph
                        .AddVideoSrcFilter(
                            new MediaFilter(MediaFilter.VideoSources.Buffer),
                            width,
                            height,
                            (AVPixelFormat)format,
                            time_base,
                            sample_aspect_ratio,
                            new AVRational())
                        .LinkTo(0,
                            filterGraph
                                .AddFilter(
                                    new MediaFilter("yadif")))
                        .LinkTo(0,
                            filterGraph
                                .AddVideoSinkFilter(
                                    new MediaFilter(MediaFilter.VideoSinks.Buffersink)));

                    filterGraph.Initialize();

                    try
                    {
                        var bmpIndex = -1;
                        var sw = new Stopwatch();
                        sw.Start();

                        var exec = new TaskParallelExecutor();
                        exec.Add(() =>
                        {
                            // FPS control
                            var framesIndex = 0;
                            var framesCount = 0L;
                            var th2_started = false;
                            var lastPts = -1L;

                            foreach (var srcPacket in reader0.ReadPacket())
                            {
                                if (token.IsCancellationRequested)
                                    return;

                                foreach (var srcFrame in videoStream.ReadFrame(srcPacket))
                                {
                                    if (token.IsCancellationRequested)
                                        return;

                                    filterGraph.Inputs.First().WriteFrame(srcFrame);

                                    foreach (var filterFrame in filterGraph.Outputs.First().ReadFrame())
                                    {
                                        //var filterFrame = srcFrame;

                                        if (!capture)
                                        {
                                            // pass event about start capturing
                                            CaptureStart?.Invoke(null, EventArgs.Empty);
                                            capture = true;
                                            timer.Start();
                                            IsRecording = true;
                                        }

                                        if (token.IsCancellationRequested)
                                            return;

                                        #region Show frame

                                        if (_framesProcessStage == 0 && framesIndex > 2 && !th2_started)
                                        {
                                            framesIndex = 0;
                                            // Get a source frame image
                                            var tm = new Stopwatch();
                                                tm.Start();
                                                th2_started = true;

                                                var mat = srcFrame.ToMat();
                                                var t2 = new Thread(() =>
                                                {
                                                    try
                                                    {
                                                        var bmp = mat.Bitmap;
                                                        _frameSource = new Bitmap(bmp.Width / 2, bmp.Height / 2);
                                                        using (var g = Graphics.FromImage(_frameSource))
                                                        {
                                                            g.DrawImage(bmp, 0, 0, bmp.Width / 2, bmp.Height / 2);
                                                        }
                                                    }
                                                    finally
                                                    {
                                                        mat.Dispose();
                                                        th2_started = false;
                                                        tm.Stop();
                                                        Console.WriteLine(tm.ElapsedMilliseconds + "ms");
                                                        _framesProcessStage = 1;
                                                    }
                                                });
                                                t2.Start();
                                        }

                                        framesIndex++;

                                        #endregion

                                        // Recording to a video file
                                        var currPts = (long)(timer.Elapsed.TotalSeconds * fps);
                                        var ts = new Stopwatch();
                                        ts.Start();

                                        //var dstMat = filterFrame.ToMat(); 
                                        //var g2 = Graphics.FromImage(dstMat.Bitmap);
                                        //foreach (var dstFrame in pixelConverter.Convert(filterFrame))
                                        try
                                        {
                                            using (var dstMat = filterFrame.ToMat())
                                            //using (var g2 = Graphics.FromImage(dstMat.Bitmap))
                                            {
                                                // Рисуем
                                                var clock = _timeCurrent;
                                                var time = _timeCurrent - _timeTimerOffset;
                                                var strike_time = _halfTime - _currentTimer;// time;
                                                var over_flag = _createOverlay && time < _halfTime;

                                                var fgea = new FrameGeneratedEventArgs(dstMat.Bitmap, strike_time, bmpIndex, over_flag);

                                                OnNewFrame(fgea);

                                                using (var dstFrame = dstMat.ToVideoFrame(opfmt))
                                                {
                                                    var fpsr = 0.0;
                                                    if (timer.Elapsed.TotalSeconds > 0)
                                                        fpsr = (double)(framesCount + 1) / timer.Elapsed.TotalSeconds;

                                                    if (fpsr >= fps - 1)
                                                    {
                                                        dstFrame.Pts = framesCount;
                                                    }
                                                    else
                                                    {
                                                        if (currPts <= lastPts) continue;
                                                        dstFrame.Pts = currPts;
                                                    }

                                                    framesCount++;

                                                    Console.WriteLine(
                                                        $@"PTS {framesCount} {currPts} {dstFrame.Pts} fps={fpsr}");

                                                    try
                                                    {
                                                        foreach (var dstPacket in writer[0].WriteFrame(dstFrame))
                                                        {
                                                            writer.WritePacket(dstPacket);
                                                        }
                                                    }
                                                    catch (Exception e)
                                                    {
                                                        Console.WriteLine(e);
                                                        //throw;
                                                    }
                                                }
                                            }
                                        }
                                        finally
                                        {
                                            lastPts = currPts;
                                        }

                                        Console.WriteLine($@"Write frame duration {ts.ElapsedMilliseconds}ms");
                                    }
                                }
                            }

                            timer.Stop();
                        });

                        exec.Add(() =>
                        {
                            long pts = 0;
                            foreach (var packet in reader1.ReadPacket())
                            {
                                if (token.IsCancellationRequested)
                                    return;

                                foreach (var frame in audioStream.ReadFrame(packet))
                                {
                                    if (token.IsCancellationRequested)
                                        return;

                                    foreach (var dstFrame in converter.Convert(frame))
                                    {
                                        if (token.IsCancellationRequested)
                                            return;

                                        pts += dstFrame.AVFrame.nb_samples;
                                        dstFrame.Pts = pts;

                                        try
                                        {
                                            foreach (var dstpacket in writer[1].WriteFrame(dstFrame))
                                            {
                                                writer.WritePacket(dstpacket);
                                            }
                                        }
                                        catch (Exception e)
                                        {
                                            Console.WriteLine(e);
                                            throw;
                                        }

                                    }
                                }
                            }
                        });

                        exec.Start(2, 0);
                    }
                    finally
                    {
                        // flush codec cache
                        writer.FlushMuxer();

                        GC.SuppressFinalize(reader0);
                        GC.SuppressFinalize(reader1);
                        GC.SuppressFinalize(writer);
                    }
                }
            }
            catch (TaskCanceledException)
            {
                //штатное завершение
            }
            catch (Exception ex)
            {
                RecordException?.Invoke(this, ex);
            }
            finally
            {
                RecordFinishing?.Invoke(this, EventArgs.Empty);
                RecordCompleted?.Invoke(this, EventArgs.Empty);

                IsRecording = false;
            }
        }

        private static void ClearDir(String dir_path)
        {
            var di = new DirectoryInfo(dir_path);
            foreach (var d in di.GetDirectories())
            {
                ClearDir(d.FullName);
            }

            foreach (var fn in di.GetFiles())
            {
                File.Delete(fn.FullName);
            }
        }

        public void PrepareResult(String tmp_dir, String fileName)
        {
            List<string> scenes_over = null;
            lock (scenes)
            {
                scenes_over = scenes
                    .Where(o => o.Step == 1)
                    .OrderBy(o => o.Index)
                    .Select(o => Path.Combine(tmp_dir, o.NameOver))
                    .ToList();
            }

            var play_list = Path.Combine(tmp_dir, "play.list");
            using (var sw = File.CreateText(play_list))
            {
                foreach (var e in scenes_over)
                {
                    var fn = "file '" + e.Replace(@"\", "/") + "'";
                    sw.WriteLine(fn);
                }
            }

            var proc_o = Ffmpeg.Joint(
                play_list,
                scenes_over,
                fileName,
                _configuration.AudioDeviceID == null);

            proc_o.Start();
            proc_o.BeginErrorReadLine();
            proc_o.WaitForExit();

            if (proc_o.ExitCode != 0)
            {
                throw new Exception("PrepareResults process completed with failure. ExitCode: " + proc_o.ExitCode);
            }

            ConcatCompleted?.Invoke(this, fileName);
        }

        public void CloseProcess(Process proc)
        {
            if (proc != null && !proc.HasExited)
            {
                GenerateConsoleCtrlEvent(ConsoleCtrlEvent.CTRL_C, proc.SessionId);
                proc.Kill();
                while (!proc.HasExited)
                {
                    Thread.Sleep(1000);
                }
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool GenerateConsoleCtrlEvent(ConsoleCtrlEvent sigevent, int dwProcessGroupId);
        public enum ConsoleCtrlEvent
        {
            CTRL_C = 0,
            CTRL_BREAK = 1,
            CTRL_CLOSE = 2,
            CTRL_LOGOFF = 5,
            CTRL_SHUTDOWN = 6
        }

        public void StopRecording()
        {
            if (!IsRecording)
                return;

            cts.Cancel();

            //CleanupInternalResources();
        }

        protected virtual void OnNewFrame(FrameGeneratedEventArgs e)
        {
            NewFrame?.Invoke(this, e);
        }
        
        #region Video/Audio source handlers
        
        public class SyncObject
        {
            public bool Mutex { get; set; }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~FfmpegCamRecorder()
        {
            Dispose(false);
        }

        void Dispose(bool disposing)
        {
            if (disposing)
            {
            }

            if (proc_enc != null && !proc_enc.HasExited)
            {
                proc_enc.Kill();
            }
        }

        #endregion
    }
}