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
        public event EventHandler<String> RecordProcess;
        public event EventHandler<double> OverlayProcess;
        public event EventHandler RecordFinishing;
        public event EventHandler RecordCompleted;
        public event EventHandler<String> ConcatCompleted;

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

        void IRecorder.SetTimerOffset(int t)
        {
            _timeTimerOffset = _timeCurrent - t;
        }

        public void CreateOverlay(bool flag)
        {
            _createOverlay = flag;
        }

        public FfmpegCamRecorder(string fileName, RecorderConfiguration configuration)
        {
            if (IsRecording || string.IsNullOrEmpty(fileName))
                return;

            _fileName = fileName;
            _configuration = configuration;
        }

        public static FfmpegCamRecorder StartRecording(
            string fileName,
            RecorderConfiguration configuration,
            EventHandler<FrameGeneratedEventArgs> _newFrame,
            EventHandler<String> _recordProcess = null,
            EventHandler<double> _overlayProcess = null,
            EventHandler _recordFinishing = null,
            EventHandler _recordCompleted = null,
            EventHandler<String> _concatCompleted = null,
            EventHandler<Exception> _recordException = null,
            EventHandler<Exception> _overlayException = null,
            EventHandler<Exception> _concatException = null)
            {
                var r = new FfmpegCamRecorder(fileName, configuration);
            
                r.NewFrame = _newFrame;
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
                return;

            DirectXDevices.Refresh();

            //"@device_pnp_\\\\?\\usb#vid_04f2&pid_b56b&mi_00#6&25ee911b&0&0000#{65e8773d-8f56-11d0-a3b9-00a0c9223196}\\global"
            var video_name = _configuration.VideoDeviceID.ToUpper();
            var video_guid = "";

            Match match = pattern_guid.Match(video_name);
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

        private void ParseM3U8(String dir, List<Scene> list)
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");

            var fileName = dir + @"\out.m3u8";
            String prefix = "out";

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
                        if (String.IsNullOrEmpty(extInf) && line.Contains("#EXTINF:"))
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
                                    Scene sc = new Scene
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
            List<Scene> not_over_list = null;
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

                not_over_list = scenes
                    .Where(o
                        => o.Step == 0
                        && (o.IsLast || o.NameImages.All(o1 => overley_list.Any(o2 => o2.Item2.Contains(o1)))))
                    .ToList();
            }

            foreach (Scene sc in not_over_list)
            {
                if (token.IsCancellationRequested)
                    return true;

                var tmp_dir_over = Path.Combine(tmp_dir, "over");
                if (!Directory.Exists(tmp_dir_over))
                    Directory.CreateDirectory(tmp_dir_over);
                else
                    ClearDir(tmp_dir_over);

                try
                {
                    //Переносим в темп для овера
                    int index = 0;
                    foreach (var fn in sc.NameImages)
                    {
                        var src = Path.Combine(tmp_dir, fn);

                        if (!File.Exists(src))
                            continue;

                        var dest = Path.Combine(tmp_dir_over, $"over{index.ToString("000000")}.png");
                        File.Move(src, dest);
                        index++;
                    }

                    var video_fn = Path.Combine(tmp_dir, sc.NameCode);
                    var result_fn = Path.Combine(tmp_dir, sc.NameOver);

                    var last_log = "";
                    Process proc_a = Ffmpeg.AttachOverlay(
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
                        if (!String.IsNullOrEmpty(e.Data))
                        {
                            last_log = e.Data;
                        }
                    };

                    proc_a.Start();
                    proc_a.BeginErrorReadLine();

                    if (!proc_a.WaitForExit(10000))
                    {
                        proc_a.Kill();
                    }

                    if (proc_a.ExitCode != 0)
                    {
                        OverlayException?.Invoke(this, new Exception(last_log));
                        break;
                    }

                    offset_segment += sc.Len;

                    OverlayProcess?.Invoke(this, offset_segment);

                    Console.WriteLine($"Over {result_fn}");

                    File.Delete(video_fn);

                    sc.Step = 1;
                }
                finally
                {
                    //Чистим
                    ClearDir(tmp_dir_over);
                    Directory.Delete(tmp_dir_over);
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
                    string tmp_dir,
                    MediaStream video_stream,
                    MediaStream audio_stream)
        {
            Task task_over = null;
            cts = new CancellationTokenSource();
            var cts_over = new CancellationTokenSource();
            offset_segment = 0f;
            scenes = new List<Scene>();
            var overley_list = new List<Tuple<int, String>>();

            try
            {
                proc_enc = Ffmpeg.CreateRecordProcess(
                    video_stream.AlterName ?? video_stream.Name,
                    audio_stream?.AlterName ?? audio_stream?.Name,
                    _configuration.VideoWidth.Value,
                    _configuration.VideoHeight.Value,
                    _configuration.VideoFrameRate.Value,
                    _configuration.VideoFrameRate.Value,
                    tmp_dir,
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

                var bmp_index = -1;
                var time_offset = 0L;
                var sw = new Stopwatch();
                sw.Start();

                //Читаем плейлист, накладываем оверлей
                task_over = Task.Factory.StartNew(() =>
                {
                    try
                    {
                        bool can_finish = false;
                        while (true)
                        {
                            List<Tuple<int, String>> overley_list_ = new List<Tuple<int, string>>();
                            lock (overley_list)
                            {
                                overley_list_.AddRange(overley_list);
                            }

                            lock (scenes)
                            {
                                int count_s = scenes.Count - 1;
                                int count_s1 = scenes.Count(o => o.Step == 1) - 1;
                                int count_b = bmp_index - 1;
                                int count = Math.Min(count_s, count_b);
                                //can_finish = count >= count_s1;
                                can_finish = scenes.Any(o => o.IsLast);
                            }

                            if (can_finish)
                            {
                                if (cts.Token.IsCancellationRequested
                                    && proc_enc.HasExited)
                                    break;
                            }

                            if (CreateOverlay(
                                tmp_dir,
                                overley_list_,
                                audio_stream == null,
                                proc_enc.HasExited,
                                cts_over.Token))
                            {
                                return;
                            }

                            Task.Delay(1000, cts_over.Token).Wait();
                        }
                    }
                    catch (TaskCanceledException)
                    {
                        //штатное завершение
                    }
                    catch
                    { }
                });

                // Ограничиваем обработку
                var taskList = new List<Task>();
                Task.Factory.StartNew(() => 
                {
                    int max = 3;
                    while (!cts.Token.IsCancellationRequested && !proc_enc.HasExited && IsRecording)
                    {
                        try
                        {
                            lock (taskList)
                            {
                                if (taskList.Count(o => o.Status == TaskStatus.Running) >= max)
                                {
                                    continue;
                                }

                                var task = taskList.FirstOrDefault(o => o.Status == TaskStatus.Created);

                                if (task != null)
                                {
                                    task.Start();
                                }
                            }
                        }
                        finally
                        {
                            Task.Delay(100, cts_over.Token).Wait();
                        }
                    }
                }, cts.Token);

                // Команды на создание оверлея
                while (!cts.Token.IsCancellationRequested && !proc_enc.HasExited && IsRecording)
                {
                    _timeCurrent = sw.ElapsedMilliseconds - _timeTimerOffset;
                    long t = _timeCurrent - time_offset;
                    if (t >= OverlayPeriod)
                    {
                        bmp_index++;
                        time_offset = _timeCurrent;
                        Console.WriteLine($"{_timeCurrent} => {t} => {bmp_index}");

                        var sw2 = new Stopwatch();
                        sw2.Start();
                        int bmp_index_ = bmp_index;

                        lock (taskList)
                        {
                            var time = _timeCurrent - _timeTimerOffset;
                            var createOverlay = _createOverlay;
                            taskList.Add(new Task(() =>
                            {
                                using (var bmp = new Bitmap(
                                    _configuration.VideoWidth.Value,
                                    _configuration.VideoHeight.Value))
                                {
                                    try
                                    {
                                        var fgea = new FrameGeneratedEventArgs(bmp, time, bmp_index_);

                                        if (createOverlay)
                                        {
                                            OnNewFrame(fgea);
                                        }

                                        var overlay_fn = Path.Combine(tmp_dir, fgea.FileName);
                                        bmp.Save(overlay_fn);

                                        lock (overley_list)
                                        {
                                            overley_list.Add(new Tuple<int, string>(bmp_index_, overlay_fn));
                                        }
                                    }
                                    catch (Exception ex)
                                    { }
                                }
                            }));
                        }

                        sw2.Stop();
                        time_offset += sw2.ElapsedMilliseconds;
                        continue;
                    }
                    Task.Delay(50, cts.Token);
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
                            var w_res = task_over.Wait(OverlayPostprocessTime);
                            //Если не завершилось за таймаут, то отменяем принудительно и ждем
                            if (!w_res)
                            {
                                cts_over.Cancel();
                                task_over.Wait();
                            }
                        }
                        else
                        {
                            task_over.Wait();
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
                    PrepareResult(tmp_dir, fileName);
                }
                catch (Exception ex)
                {
                    ConcatException?.Invoke(this, ex);
                }
                finally
                {
                    //Чистим
                    ClearDir(tmp_dir);
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
            List<String> scenes_over = null;
            lock (scenes)
            {
                scenes_over = scenes
                    .Where(o => o.Step == 1)
                    .OrderBy(o => o.Index)
                    .Select(o => Path.Combine(tmp_dir, o.NameOver))
                    .ToList<String>();
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

            Process proc_o = Ffmpeg.Joint(
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