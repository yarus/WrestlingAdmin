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

        void IRecorder.SetTimerOffset(int t)
        {
            _timeTimerOffset = _timeCurrent - t;
        }

        public void CreateOverlay(bool flag)
        {
            _createOverlay = flag;
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
            if (IsRecording || string.IsNullOrEmpty(_fileName))
            {
                return;
            }

            DirectXDevices.Refresh();

            //"@device_pnp_\\\\?\\usb#vid_04f2&pid_b56b&mi_00#6&25ee911b&0&0000#{65e8773d-8f56-11d0-a3b9-00a0c9223196}\\global"
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

            var video_stream = DirectXDevices.List.FirstOrDefault(o => o.Name.ToUpper().Contains(video_guid));

            MediaStream audio_stream = null;
            if (_configuration.AudioDeviceID != null)
            {
                var audio_guid = _configuration.AudioDeviceID.ToString().ToUpper();
                audio_stream = DirectXDevices.List.FirstOrDefault(o => o.Name.ToUpper().Contains(audio_guid));
                if (audio_stream == null)
                    audio_stream = DirectXDevices.List.FirstOrDefault(o => o.StreamType == StreamTypeEnum.Audio);
            }

            var dir = Path.GetDirectoryName(_fileName);
            var name = Path.GetFileNameWithoutExtension(_fileName);
            var tmp_dir = Path.Combine(dir, "temp", name);
            if (!Directory.Exists(tmp_dir))
            {
                Directory.CreateDirectory(tmp_dir);
            }
            else
            {
                ClearDir(tmp_dir);
            }

            Task.Factory.StartNew(() =>
            {
                TaskRecordVideoAndCreateOverlay(_fileName, tmp_dir, video_stream, audio_stream);
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

        private void TaskRecordVideoAndCreateOverlay(
            string fileName,
            string tmpDir,
            MediaStream video_stream,
            MediaStream audio_stream)
        {
            Thread task_over = null;
            cts = new CancellationTokenSource();
            var cts_over = new CancellationTokenSource();
            offset_segment = 0f;
            scenes = new List<Scene>();
            var overley_list = new List<Tuple<int, string>>();

            try
            {
                proc_enc = Ffmpeg.CreateRecordProcess(
                    video_stream.AlterName ?? video_stream.Name,
                    audio_stream?.AlterName ?? audio_stream?.Name,
                    _configuration.VideoWidth.Value,
                    _configuration.VideoHeight.Value,
                    _configuration.VideoFrameRate.Value,
                    _configuration.VideoFrameRate.Value,
                    tmpDir,
                    _configuration.VBitrate,
                    _configuration.VQuality,
                    _configuration.ABitrate,
                    _configuration.AFrequency,
                    _configuration.VCodec,
                    _configuration.ACodec,
                    _configuration.Preset);

                IsRecording = true;

                proc_enc.ErrorDataReceived += (object sender, DataReceivedEventArgs e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        var time = "";
                        var match = pattern_time.Match(e.Data);
                        while (match.Success)
                        {
                            time = match.Groups[1].Value.ToUpper();
                            match = match.NextMatch();

                            RecordProcess?.Invoke(this, time);
                        }
                        match = pattern_rate.Match(e.Data);
                        while (match.Success)
                        {
                            time += " x" + match.Groups[1].Value.ToUpper();
                            match = match.NextMatch();

                            Console.WriteLine("Record: " + time);

                            RecordProcess?.Invoke(this, time);
                        }
                        //Console.WriteLine("Record: " + e.Data);
                    }
                };

                proc_enc.Start();
                proc_enc.BeginErrorReadLine();

                var bmpIndex = -1;
                var sw = new Stopwatch();
                sw.Start();

                //Читаем плейлист, накладываем оверлей
                task_over = new Thread(() =>
                {
                    try
                    {
                        bool can_finish = false;
                        while (true)
                        {
                            var overley_list_ = new List<Tuple<int, string>>();
                            lock (overley_list)
                            {
                                overley_list_.AddRange(overley_list);
                            }

                            lock (scenes)
                            {
                                int count_s = scenes.Count - 1;
                                int count_s1 = scenes.Count(o => o.Step == 1) - 1;
                                int count_b = bmpIndex - 1;
                                int count = Math.Min(count_s, count_b);
                                //can_finish = count >= count_s1;
                                can_finish = scenes.Any(o => o.IsLast);
                            }

                            if (can_finish)
                            {
                                if (cts.Token.IsCancellationRequested
                                    && proc_enc.HasExited)
                                {
                                    break;
                                }
                            }

                            if (CreateOverlay(
                                tmpDir,
                                overley_list_,
                                audio_stream == null,
                                proc_enc.HasExited,
                                cts_over.Token))
                            {
                                return;
                            }

                            //Task.Delay(100, cts_over.Token).Wait();
                            Thread.Sleep(100);
                        }
                    }
                    catch (TaskCanceledException)
                    {
                        //штатное завершение
                    }
                    catch
                    { }
                })
                {
                    IsBackground = true
                };
                task_over.Start();

                int overlay_main_loop = 0;
                int overlay_running = 0;
                int overlay_run_index = 0;

                // Ограничиваем обработку
                var taskList = new List<Thread>();
                var thOverMain = new Thread(() =>
                {
                    Console.WriteLine($"->Started overlay manager!");

                    int max = 1;
                    while (!cts.Token.IsCancellationRequested && !proc_enc.HasExited && IsRecording)
                    {
                        overlay_main_loop++;

                        try
                        {
                            int running = overlay_running;

                            // Сколько задач запущено. Если больше max, то ждем еще
                            if (running >= max)
                            {
                                continue;
                            }

                            Thread task = null;
                            lock (taskList)
                            {
                                // Первая попавшаяся не запущенная задача 
                                task = taskList.FirstOrDefault(o => (o.ThreadState & System.Threading.ThreadState.Unstarted) > 0);
                            }

                            // Запускаем ее
                            if (task != null)
                            {
                                overlay_running++;
                                overlay_run_index++;
                                //Console.WriteLine($"-->Starting thread index={overlay_run_index}");
                                task.Start();
                            }

                        }
                        finally
                        {
                            Thread.Sleep(100);
                        }
                    }
                })
                {
                    IsBackground = true
                };
                thOverMain.Start();

                var old_time = 0L;

                // Команды на создание оверлея
                while (!cts.Token.IsCancellationRequested && !proc_enc.HasExited && IsRecording)
                {
                    long t1 = sw.ElapsedMilliseconds;
                    _timeCurrent = t1;

                    long t = t1 - _timeOffset;
                    if (t >= OverlayPeriod)
                    {
                        bmpIndex++;
                        _timeOffset = t1;
                        Console.WriteLine($"{_timeCurrent} => {t} => {bmpIndex}");

                        var sw2 = new Stopwatch();
                        sw2.Start();
                        int bmpIndex_ = bmpIndex;

                        var clock = _timeCurrent;
                        var time = _timeCurrent - _timeTimerOffset;
                        var createOverlay = _createOverlay;

                        if (Math.Abs(time - old_time) > 1000)
                        {
                            Console.WriteLine($"### time = {time} {_timeCurrent} {_timeTimerOffset}");
                            old_time = time;
                        }

                        var thread = new Thread(() =>
                        {
                            try
                            {
                                using (var bmp = new Bitmap(
                                        _configuration.VideoWidth.Value,
                                        _configuration.VideoHeight.Value))
                                {
                                    try
                                    {
                                        var strike_time = _halfTime - time;
                                        var over_flag = createOverlay && time < _halfTime;

                                        /*
                                        if (!over_flag)
                                            strike_time = 0;
                                            */

                                        var fgea = new FrameGeneratedEventArgs(bmp, strike_time, bmpIndex_, over_flag);

                                        //if (over_flag)
                                        {
                                            OnNewFrame(fgea);
                                        }

                                        var overlay_fn = Path.Combine(tmpDir, fgea.FileName);

                                        bmp.Save(overlay_fn, Scene.ImageOverFmt);

                                        lock (overley_list)
                                        {
                                            overley_list.Add(new Tuple<int, string>(bmpIndex_, overlay_fn));
                                        }

                                        Console.WriteLine($"-->Created over for {TimeSpan.FromMilliseconds(clock).ToString("m\\:ss")} index={bmpIndex} ({over_flag} = {TimeSpan.FromMilliseconds(strike_time).ToString("m\\:ss")})");
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine($"Exception: {ex.Message}");
                                    }
                                }
                            }
                            finally
                            {
                                lock (taskList)
                                {
                                    taskList.Remove(Thread.CurrentThread);
                                }
                                overlay_running--;
                            }
                        })
                        {
                            IsBackground = true
                        };

                        lock (taskList)
                        {
                            taskList.Add(thread);
                        }

                        sw2.Stop();
                        _timeOffset += sw2.ElapsedMilliseconds;
                        continue;
                    }

                    Thread.Sleep(50);
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

                //Завершаем процесс записи
                CloseProcess(proc_enc);

                //Ждем завершения оверлея
                if (task_over != null)
                {
                    try
                    {
                        if (OverlayPostprocessTime >= 0)
                        {
                            var w_res = task_over.Join(OverlayPostprocessTime);
                            //Если не завершилось за таймаут, то отменяем принудительно и ждем
                            if (!w_res)
                            {
                                cts_over.Cancel();
                                task_over.Join();
                            }
                        }
                        else
                        {
                            task_over.Join();
                        }
                    }
                    catch (AggregateException)
                    {
                        //Если задача была уже завершена. Ничего не делаем
                    }
                    catch (Exception ex)
                    {
                        OverlayException?.Invoke(this, ex);
                    }
                }

                RecordCompleted?.Invoke(this, EventArgs.Empty);

                try
                {
                    PrepareResult(tmpDir, fileName);
                }
                catch (Exception ex)
                {
                    ConcatException?.Invoke(this, ex);
                }
                finally
                {
                    //Чистим
                    ClearDir(tmpDir);
                }

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

            if (proc_o.ExitCode != 0 && ConcatException != null)
                ConcatException(this, new Exception(""));

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

            IsRecording = false;
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