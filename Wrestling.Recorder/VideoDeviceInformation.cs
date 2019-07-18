namespace Wrestling.Recorder
{
    public sealed class VideoDeviceInformation
    {
        /// <summary>
        /// Gets or sets the display name of the video device source.
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// Gets or sets the USB Id / Moniker string of the video device source.
        /// </summary>
        public string UsbId { get; set; }

        public int Width { get; set; }

        public int Height { get; set; }

        public int FrameRate { get; set; } // 44100
        public string Preset { get; set; }
    }
}