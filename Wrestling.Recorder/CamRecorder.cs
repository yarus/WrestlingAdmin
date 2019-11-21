using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Threading;
using Accord.Audio;
using Accord.DirectSound;
using Accord.Math;
using Accord.Video;
using Accord.Video.DirectShow;
using Accord.Video.FFMPEG;

namespace Wrestling.Recorder
{
    public class CamRecorder : IDisposable, IRecorder
    {
        private const int DEFAULT_FRAME_RATE = 30;
        private const int DEFAULT_HEIGHT = 480;
        private const int DEFAULT_WIDTH = 640;
        private const string DEFAULT_PRESET = "veryfast";
        private const int DEFAULT_SAMPLE_RATE = 44100;

        #region Fields

        private long _startTick = 0;
        private VideoCaptureDevice _videoCaptureDevice;
        private VideoFileWriter _videoWriter;
        private AudioSourceMixer _audioMixer;
        private DispatcherTimer _timer;
        private RecorderConfiguration _configuration;
        private string _fileName;

        private int _numberOfTries;
        private bool _isCapturingConfirmed;
        //private WaveEncoder _encoder;

        private readonly Object _syncObj = new Object();

        #endregion

        public DateTime RecordingStartTime { get; private set; }

        public bool IsRecording { get; private set; }

        public CamRecorder(RecorderConfiguration configuration, string fileName)
        {
            _configuration = configuration;
            _fileName = fileName;
        }

        public static CamRecorder StartRecording(RecorderConfiguration configuration, string fileName)
        {
            var recorder = new CamRecorder(configuration, fileName);
            recorder.StartRecording();
            return recorder;
        }

        public void StartRecording()
        {
            if (IsRecording && _isCapturingConfirmed || string.IsNullOrEmpty(_fileName))
                return;

            if (string.IsNullOrEmpty(_configuration.VideoDeviceID))
                return;

            _videoCaptureDevice = GetVideoDevice(_configuration.VideoDeviceID);

            if (_videoCaptureDevice == null)
                return;

            InitRecordingComponents(_configuration);

            _startTick = 0;

            _videoCaptureDevice.Start();

            RecordingStartTime = DateTime.Now;
            IsRecording = true;

            StartRetryTimer();
        }

        public void StopRecording()
        {
            if (!IsRecording)
                return;

            IsRecording = false;

            CleanupInternalResources();
        }

        public void SetMaxSeconds(int seconds)
        {
            
        }

        public event EventHandler<FrameGeneratedEventArgs> NewFrame;
        public event EventHandler<string> ConcatCompleted;
        public event EventHandler<Exception> ConcatException;

        protected virtual void OnNewFrame(FrameGeneratedEventArgs e)
        {
            EventHandler<FrameGeneratedEventArgs> handler = NewFrame;

            handler?.Invoke(this, e);
        }

        #region Private methods

        private void InitRecordingComponents(RecorderConfiguration configuration)
        {
            int audioFrameSize = 4096 * 10;
            int audioBitRate = 320 * 1000;

            try
            {
                _videoWriter = GetVideoWriter(configuration);

                var res = _videoCaptureDevice.VideoCapabilities.FirstOrDefault(p =>
                    p.FrameSize.Width == configuration.VideoWidth && p.FrameSize.Height == configuration.VideoHeight &&
                    p.AverageFrameRate == configuration.VideoFrameRate);

                if (res != null)
                {
                    _videoCaptureDevice.VideoResolution = res;
                }
                
                if (_videoWriter != null)
                {
                    _audioMixer = GetAudioMixer(configuration, audioFrameSize);
                    if (_audioMixer != null)
                    {
                        AddAudioMixerToVideoWriter(_audioMixer, _videoWriter, audioFrameSize, audioBitRate);
                    }

                    _videoWriter.Open(_fileName);

                    /*
                    var audioFileName = _fileName.Substring(0, _fileName.Length - 4) + ".wav";

                    //using(var stream = new MemoryStream())
                    using (var stream = new FileStream(audioFileName, FileMode.Create))
                    {
                        _encoder = new WaveEncoder(stream);
                    }
                    */
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                CleanupInternalResources();
            }
        }

        private void CleanupInternalResources()
        {
            if (_videoCaptureDevice != null)
            {
                if (_videoCaptureDevice.IsRunning)
                {
                    _videoCaptureDevice.SignalToStop();
                    _videoCaptureDevice.WaitForStop();
                }
                _videoCaptureDevice.NewFrame -= _videoCaptureDevice_NewFrame;
                _videoCaptureDevice = null;
            }

            if (_audioMixer != null)
            {
                foreach (var source in _audioMixer.Sources)
                {
                    if (source.IsRunning)
                    {
                        source.SignalToStop();
                    }
                    source.Dispose();
                }

                if (_audioMixer.IsRunning)
                {
                    _audioMixer.SignalToStop();
                }
                _audioMixer.NewFrame -= _audioMixer_NewFrame;
                _audioMixer.Dispose();
                _audioMixer = null;
            }

            if (_videoWriter != null)
            {
                if (_videoWriter.IsOpen)
                {
                    _videoWriter.Close();
                }
                _videoWriter.Dispose();
                _videoWriter = null;
            }

            /*
            if (_encoder != null)
            {
                _encoder.Close();
                _encoder = null;
            }
            */
        }

        private VideoCaptureDevice GetVideoDevice(string usbId)
        {
            VideoCaptureDevice device = null;

            try
            {
                device = new VideoCaptureDevice(usbId);
                device.NewFrame += _videoCaptureDevice_NewFrame;
                device.VideoSourceError += _videoCaptureDevice_VideoSourceError;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            return device;
        }

        private VideoFileWriter GetVideoWriter(RecorderConfiguration configuration)
        {
            int interval = (int)Math.Round(1000 / (double)(configuration.VideoFrameRate ?? DEFAULT_FRAME_RATE));
            Rational framerate = new Rational(1000, interval);
            int height = configuration.VideoHeight ?? DEFAULT_HEIGHT;
            int width = configuration.VideoWidth ?? DEFAULT_WIDTH;
            int videoBitRate = 1200 * 1000; // height * width * (configuration.VideoFrameRate ?? DEFAULT_FRAME_RATE)

            var videoWriter = new VideoFileWriter
            {
                BitRate = videoBitRate,
                FrameRate = framerate,
                Width = width,
                Height = height,
                VideoCodec = VideoCodec.Default
            };

            videoWriter.VideoOptions["crf"] = "18"; // visually lossless
            videoWriter.VideoOptions["preset"] = string.IsNullOrEmpty(configuration.Preset) ? DEFAULT_PRESET : configuration.Preset;
            videoWriter.VideoOptions["tune"] = "zerolatency";
            videoWriter.VideoOptions["x264opts"] = "no-mbtree:sliced-threads:sync-lookahead=0";

            return videoWriter;
        }

        #endregion

        #region Audio devices methods

        private void AddAudioMixerToVideoWriter(AudioSourceMixer mixer, VideoFileWriter videoWriter, int frameSize, int bitRate)
        {
            videoWriter.AudioBitRate = bitRate;
            videoWriter.AudioCodec = AudioCodec.Aac;
            videoWriter.AudioLayout = AudioLayout.Mono;// mixer.NumberOfChannels == 1 ? AudioLayout.Mono : AudioLayout.Stereo;
            videoWriter.FrameSize = frameSize;
            videoWriter.SampleRate = mixer.SampleRate;
        }

        private AudioCaptureDevice GetAudioDeviceData(Guid id)
        {
            AudioCaptureDevice device = null;

            try
            {
                device = new AudioCaptureDevice(id);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                device = null;
            }

            return device;
        }

        private List<AudioCaptureDevice> GetAudioDevices(IEnumerable<Guid> ids, int desiredFrameSize)
        {
            var audioDevices = new List<AudioCaptureDevice>();

            foreach (var id in ids)
            {
                AudioCaptureDevice deviceData = GetAudioDeviceData(id);

                if (deviceData == null)
                {
                    continue;
                }

                deviceData.AudioSourceError += DeviceData_AudioSourceError;
                deviceData.Format = SampleFormat.Format32BitIeeeFloat;
                deviceData.SampleRate = DEFAULT_SAMPLE_RATE;
                deviceData.DesiredFrameSize = desiredFrameSize;
                deviceData.Start();

                audioDevices.Add(deviceData);
            }

            return audioDevices;
        }

        private AudioSourceMixer GetAudioMixer(RecorderConfiguration configuration, int desiredFrameSize)
        {
            if (!configuration.AudioDeviceID.HasValue)
            {
                return null;
            }

            AudioSourceMixer mixer = null;

            // TODO: This code can be adjusted to provide list of audio souces instead of 1 device
            var audioDevices = GetAudioDevices(new List<Guid>{configuration.AudioDeviceID.Value}, desiredFrameSize); 

            if (audioDevices.Count > 0)
            {
                mixer = new AudioSourceMixer(audioDevices);
                mixer.NewFrame += _audioMixer_NewFrame;
                mixer.AudioSourceError += DeviceData_AudioSourceError;
                mixer.Start();
            }

            return mixer;
        }

        #endregion

        #region Retry timer

        private void StartRetryTimer()
        {
            _isCapturingConfirmed = false;

            _timer?.Stop();

            _timer = new DispatcherTimer();
            _timer.Tick += TimerOnTick;
            _timer.Interval = new TimeSpan(0, 0, 0, 2);
            _timer.Start();
        }

        private void TimerOnTick(object sender, EventArgs e)
        {
            _timer.Stop();

            if (!_isCapturingConfirmed && _numberOfTries < 2)
            {
                _numberOfTries++;
                StopRecording();
                StartRecording();
            }
        }

        #endregion

        #region Video/Audio source handlers

        private void _videoCaptureDevice_VideoSourceError(object sender, VideoSourceErrorEventArgs eventArgs)
        {
            if (!IsRecording)
                return;

            StopRecording();
        }

        private void DeviceData_AudioSourceError(object sender, AudioSourceErrorEventArgs e)
        {
            StopRecording();
        }

        private void _videoCaptureDevice_NewFrame(object sender, Accord.Video.NewFrameEventArgs eventArgs)
        {
            if (!IsRecording) return;

            var bitmap = eventArgs.Frame.Clone() as Bitmap;

            long currentTick = DateTime.Now.Ticks;

            if (_startTick == 0)
            {
                _startTick = currentTick;
            }

            //OnNewFrame(new FrameGeneratedEventArgs(bitmap, ));

            _isCapturingConfirmed = true;
            _numberOfTries = 0;

            var offset = new TimeSpan(currentTick - _startTick);

            lock (_syncObj) // Save the frame to the video file.
            {
                _videoWriter.WriteVideoFrame(bitmap, offset);
            }
        }

        private void _audioMixer_NewFrame(object sender, Accord.Audio.NewFrameEventArgs e)
        {
            if (!IsRecording) return;

            lock (_syncObj) // Save the frame to the video file.
            {
                //_encoder.Encode(e.Signal);

                _videoWriter.WriteAudioFrame(e.Signal);
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~CamRecorder()
        {
            Dispose(false);
        }

        void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_audioMixer != null)
                {
                    foreach (var source in _audioMixer.Sources)
                    {
                        source.Dispose();
                    }

                    _audioMixer.Dispose();
                    _audioMixer = null;
                }

                if (_videoWriter != null)
                {
                    _videoWriter.Dispose();
                    _videoWriter = null;
                }
            }
        }

        public void SetTimerOffset(int t)
        {
        }

        public void CreateOverlay(bool flag)
        {
            throw new NotImplementedException();
        }

        public void SetMainSecond(int t)
        {
            
        }

        #endregion
    }
}