using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace Wrestling.UI.Material.ScoreScreen
{
    public partial class WinnerAnnouncementView : UserControl
    {
        public WinnerAnnouncementView()
        {
            InitializeComponent();
            FitHeaderText();
        }

        private void OnHeaderTextUpdated(object sender, DataTransferEventArgs e) => FitHeaderText();

        private void FitHeaderText()
        {
            if (WinnerRound == null || WinnerCategory == null) return;

            // Both fields share one font size, while the round stays at the screen centre.
            var scale = double.PositiveInfinity;
            foreach (var text in new[] { WinnerRound, WinnerCategory })
            {
                var measured = new FormattedText(string.IsNullOrEmpty(text.Text) ? " " : text.Text, CultureInfo.CurrentUICulture, text.FlowDirection,
                    new Typeface(text.FontFamily, text.FontStyle, text.FontWeight, text.FontStretch),
                    100, text.Foreground, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                var availableWidth = text == WinnerRound ? 480 : 576;
                scale = Math.Min(scale, Math.Min(availableWidth / measured.WidthIncludingTrailingWhitespace, 80 / measured.Height));
            }
            var fontSize = Math.Max(1, 100 * scale * .995);
            WinnerRound.FontSize = WinnerCategory.FontSize = fontSize;
        }
    }
}
