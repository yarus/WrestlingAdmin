using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace Wrestling.UI.Material.Tournament.Print
{
    internal sealed class BulkPdfExportJob
    {
        public string FileName { get; set; }
        public bool Landscape { get; set; }
        public Func<FrameworkElement> ViewFactory { get; set; }
    }

    internal sealed class BulkBracketPdfExporter
    {
        private const double DipsPerPoint = 96.0 / 72.0;
        private const double A4ShortSidePoints = 595.28;
        private const double A4LongSidePoints = 841.89;
        private const double HorizontalMarginPoints = 36;
        private const double VerticalMarginPoints = 36;
        // 150 DPI keeps text crisp on print but caps the bitmap pixel height
        // well below WPF's RenderTargetBitmap practical limit (~16K px).
        // At 300 DPI a long Personal Results view (13K+ DIP) produced a 41K-px
        // bitmap and WPF silently dropped pixels past ~16K, leaving blank
        // trailing pages with the footer at the very bottom.
        private const double RenderDpi = 150.0;

        public sealed class ExportResult
        {
            public int Succeeded { get; set; }
            public int Skipped { get; set; }
            public List<string> Failures { get; } = new List<string>();
        }

        // Runs the entire export on a dedicated STA thread (RenderTargetBitmap
        // requires STA). The render thread keeps the main UI thread free so
        // the progress spinner keeps animating during the export.
        public Task<ExportResult> ExportAsync(
            IList<BulkPdfExportJob> jobs,
            string outputDirectory,
            IProgress<string> progress = null)
        {
            var snapshot = jobs?.ToList() ?? new List<BulkPdfExportJob>();
            var tcs = new TaskCompletionSource<ExportResult>(TaskCreationOptions.RunContinuationsAsynchronously);

            var thread = new Thread(() =>
            {
                var result = new ExportResult();
                try
                {
                    foreach (var job in snapshot)
                    {
                        if (job?.ViewFactory == null || string.IsNullOrEmpty(job.FileName))
                        {
                            result.Skipped++;
                            continue;
                        }

                        progress?.Report(job.FileName);

                        try
                        {
                            var view = job.ViewFactory();
                            if (view == null)
                            {
                                result.Skipped++;
                                continue;
                            }

                            RenderViewToPdf(view, Path.Combine(outputDirectory, job.FileName), job.Landscape);
                            result.Succeeded++;
                        }
                        catch (Exception ex)
                        {
                            result.Failures.Add($"{job.FileName}: {ex.Message}");
                        }
                    }

                    tcs.TrySetResult(result);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
                finally
                {
                    // Shut down any Dispatcher implicitly created on this thread
                    // so the thread can exit cleanly.
                    Dispatcher.CurrentDispatcher.InvokeShutdown();
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Name = "BulkPdfExportRender";
            thread.Start();

            return tcs.Task;
        }

        private static void RenderViewToPdf(FrameworkElement view, string outputPath, bool landscape)
        {
            var pageWidthPoints = landscape ? A4LongSidePoints : A4ShortSidePoints;
            var pageHeightPoints = landscape ? A4ShortSidePoints : A4LongSidePoints;

            var imageableWidthDip = (pageWidthPoints - HorizontalMarginPoints * 2) * DipsPerPoint;
            var imageableHeightDip = (pageHeightPoints - VerticalMarginPoints * 2) * DipsPerPoint;
            var dpiScale = RenderDpi / 96.0;

            // Render fully off-tree. Hosting the view in a Window — even an
            // off-screen one — caps the visual tree at the screen working-area
            // height: WPF re-arranges descendants on every dispatcher tick and
            // the cap then sticks to their cached DesiredSize. Off-tree there's
            // no host to impose that cap. ListView items still realize because
            // every ItemsControl in the print views sets
            // `VirtualizingPanel.IsVirtualizing="False"` — generators run during
            // Measure, no Loaded events required.
            view.Width = imageableWidthDip;

            // Two measure passes: the first realizes ItemsControl containers,
            // the second observes their now-final DesiredSizes. Width must be
            // bounded — passing infinity lets content collapse to its natural
            // narrow width and ruins the height calculation.
            view.Measure(new Size(imageableWidthDip, double.PositiveInfinity));
            view.Measure(new Size(imageableWidthDip, double.PositiveInfinity));

            var measuredHeight = Math.Max(view.DesiredSize.Height, 1);
            view.Arrange(new Rect(0, 0, imageableWidthDip, measuredHeight));
            view.UpdateLayout();

            // Arrange + UpdateLayout can trigger one more layout pass — bracket
            // cell heights come from a converter that resolves only after the
            // first arrange, so the post-arrange DesiredSize / ActualHeight is
            // slightly larger than what we passed to Arrange. Use the largest
            // observed height so the bitmap captures the fully-settled content.
            measuredHeight = Math.Max(measuredHeight, view.DesiredSize.Height);
            measuredHeight = Math.Max(measuredHeight, view.ActualHeight);

            var bitmap = new RenderTargetBitmap(
                (int)Math.Ceiling(imageableWidthDip * dpiScale),
                (int)Math.Ceiling(measuredHeight * dpiScale),
                RenderDpi, RenderDpi, PixelFormats.Pbgra32);
            bitmap.Render(view);

            var pageHeightPx = (int)Math.Floor(imageableHeightDip * dpiScale);
            var totalHeightPx = bitmap.PixelHeight;
            var totalWidthPx = bitmap.PixelWidth;

            using (var document = new PdfDocument())
            {
                document.Info.Title = Path.GetFileNameWithoutExtension(outputPath);

                var keepAlive = new List<MemoryStream>();

                int top = 0;
                while (top < totalHeightPx)
                {
                    int rawBottom = Math.Min(top + pageHeightPx, totalHeightPx);
                    int bottom = rawBottom < totalHeightPx
                        ? FindCleanBreakRow(bitmap, top + (int)(pageHeightPx * 0.6), rawBottom)
                        : rawBottom;
                    int height = bottom - top;
                    var slice = new CroppedBitmap(bitmap, new Int32Rect(0, top, totalWidthPx, height));

                    var pngStream = new MemoryStream();
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(slice));
                    encoder.Save(pngStream);
                    pngStream.Position = 0;
                    keepAlive.Add(pngStream);

                    var page = document.AddPage();
                    page.Size = PdfSharp.PageSize.A4;
                    page.Orientation = landscape ? PdfSharp.PageOrientation.Landscape : PdfSharp.PageOrientation.Portrait;

                    var image = XImage.FromStream(pngStream);

                    using (var gfx = XGraphics.FromPdfPage(page))
                    {
                        var sliceHeightPoints = height / dpiScale / DipsPerPoint;
                        gfx.DrawImage(
                            image,
                            HorizontalMarginPoints,
                            VerticalMarginPoints,
                            pageWidthPoints - HorizontalMarginPoints * 2,
                            sliceHeightPoints);
                    }

                    top = bottom;
                }

                document.Save(outputPath);

                foreach (var stream in keepAlive)
                    stream.Dispose();
            }
        }

        // Scans bottom-up within [topLine..bottomLine) for a row of low color
        // variance (whitespace) and returns its index. Falls back to bottomLine
        // if nothing clean is found, so we never make zero-progress slices.
        private static int FindCleanBreakRow(RenderTargetBitmap bmp, int topLine, int bottomLine)
        {
            const double deviationThreshold = 1500.0;

            int width = bmp.PixelWidth;
            if (width <= 0 || bottomLine <= topLine) return bottomLine;

            var rowBuffer = new byte[width * 4];

            for (int i = bottomLine - 1; i > topLine; i--)
            {
                bmp.CopyPixels(new Int32Rect(0, i, width, 1), rowBuffer, width * 4, 0);

                long sum = 0;
                for (int c = 0; c < width; c++)
                {
                    int o = c * 4;
                    int pixel = (rowBuffer[o + 2] << 16) | (rowBuffer[o + 1] << 8) | rowBuffer[o];
                    sum += pixel;
                }
                double avg = sum / (double)width;

                double variance = 0;
                for (int c = 0; c < width; c++)
                {
                    int o = c * 4;
                    int pixel = (rowBuffer[o + 2] << 16) | (rowBuffer[o + 1] << 8) | rowBuffer[o];
                    double diff = pixel - avg;
                    variance += diff * diff;
                }

                if (Math.Sqrt(variance / width) < deviationThreshold)
                {
                    return i;
                }
            }

            return bottomLine;
        }

        public static string MakeSafeFileName(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "group";

            var invalid = Path.GetInvalidFileNameChars();
            var chars = raw.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
            var cleaned = new string(chars).Trim();
            return string.IsNullOrEmpty(cleaned) ? "group" : cleaned;
        }
    }
}
