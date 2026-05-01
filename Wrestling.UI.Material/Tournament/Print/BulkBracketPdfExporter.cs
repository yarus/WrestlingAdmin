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
            // Trim trailing whitespace before slicing. WPF layout often leaves a
            // strip of blank pixels at the bottom of the rendered StackPanel
            // (footer margin + StackPanel slack). Without trimming, even a few
            // pixels past the page boundary produce a fully-blank A4 second
            // page in the PDF. Scan upward from the last row until we find any
            // dark pixel.
            var totalHeightPx = Math.Max(1, FindLastContentRow(bitmap) + 1);
            // If the trimmed content overflows a single page by ≤25%, clamp to
            // one page. WPF layout slack (StackPanel padding past the visible
            // footer, ListView trailing separators) routinely pushes the
            // bitmap a few dozen DIPs past the page boundary. Without this
            // clamp the slicer dutifully produces an A4 second page with no
            // visible content. Genuine two-page brackets overflow by 50%+ so
            // they aren't affected.
            if (totalHeightPx > pageHeightPx && totalHeightPx <= (int)(pageHeightPx * 1.25))
            {
                totalHeightPx = pageHeightPx;
            }
            var totalWidthPx = bitmap.PixelWidth;

            // First, plan out the page slices. Then drop any trailing slices that
            // are visually blank — these arise when WPF layout slack pushes a
            // few dark pixels past the page boundary (see IsSliceMostlyBlank
            // for the row-density heuristic). Doing this up front means we
            // never call PdfDocument.AddPage for blank pages.
            var slices = new List<(int top, int height)>();
            {
                int top = 0;
                while (top < totalHeightPx)
                {
                    int rawBottom = Math.Min(top + pageHeightPx, totalHeightPx);
                    int bottom = rawBottom < totalHeightPx
                        ? FindCleanBreakRow(bitmap, top + (int)(pageHeightPx * 0.6), rawBottom)
                        : rawBottom;
                    slices.Add((top, bottom - top));
                    top = bottom;
                }
                while (slices.Count > 1 && IsSliceMostlyBlank(bitmap, slices[slices.Count - 1].top, slices[slices.Count - 1].height))
                {
                    slices.RemoveAt(slices.Count - 1);
                }
            }

            using (var document = new PdfDocument())
            {
                document.Info.Title = Path.GetFileNameWithoutExtension(outputPath);

                var keepAlive = new List<MemoryStream>();

                foreach (var (sliceTop, sliceHeight) in slices)
                {
                    var slice = new CroppedBitmap(bitmap, new Int32Rect(0, sliceTop, totalWidthPx, sliceHeight));

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
                        var sliceHeightPoints = sliceHeight / dpiScale / DipsPerPoint;
                        gfx.DrawImage(
                            image,
                            HorizontalMarginPoints,
                            VerticalMarginPoints,
                            pageWidthPoints - HorizontalMarginPoints * 2,
                            sliceHeightPoints);
                    }
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

        private const int ContentDarkThreshold = 240;
        private const int ContentAlphaThreshold = 32;

        // Returns the index of the last row with at least minContentPixels of
        // non-near-white pixels, or -1 if no such row exists. Requiring multiple
        // dark pixels per row defeats sub-pixel anti-aliasing artifacts and
        // single-pixel layout-rounding leftovers that occasionally outlive the
        // visible content area. Pbgra32 is premultiplied; a fully-transparent
        // pixel reads as (0,0,0,0) and we skip it via the alpha threshold.
        private static int FindLastContentRow(RenderTargetBitmap bmp)
        {
            int width = bmp.PixelWidth;
            int height = bmp.PixelHeight;
            if (width <= 0 || height <= 0) return -1;

            const int minContentPixels = 3;

            var rowBuffer = new byte[width * 4];

            for (int i = height - 1; i >= 0; i--)
            {
                bmp.CopyPixels(new Int32Rect(0, i, width, 1), rowBuffer, width * 4, 0);
                int contentPixels = 0;
                for (int c = 0; c < width; c++)
                {
                    int o = c * 4;
                    byte a = rowBuffer[o + 3];
                    if (a < ContentAlphaThreshold) continue;
                    byte b = rowBuffer[o];
                    byte g = rowBuffer[o + 1];
                    byte r = rowBuffer[o + 2];
                    if (r < ContentDarkThreshold || g < ContentDarkThreshold || b < ContentDarkThreshold)
                    {
                        contentPixels++;
                        if (contentPixels >= minContentPixels) return i;
                    }
                }
            }
            return -1;
        }

        // Slice is "mostly blank" if fewer than minContentRows rows contain at
        // least minPixelsPerRow dark pixels each. Row-density beats raw-pixel
        // counting because a single thin separator line (which can survive the
        // FindLastContentRow trim) gives many dark pixels in just one row —
        // looks like content by total count, but won't satisfy "≥3 rows".
        // Real content (text, table grid, stamps) spans dozens of rows.
        private static bool IsSliceMostlyBlank(RenderTargetBitmap bmp, int top, int height)
        {
            int width = bmp.PixelWidth;
            if (width <= 0 || height <= 0) return true;

            const int minContentRows = 3;
            const int minPixelsPerRow = 10;
            int bmpHeight = bmp.PixelHeight;

            var rowBuffer = new byte[width * 4];
            int contentRows = 0;

            for (int i = top; i < top + height && i < bmpHeight; i++)
            {
                bmp.CopyPixels(new Int32Rect(0, i, width, 1), rowBuffer, width * 4, 0);
                int rowDarkPixels = 0;
                for (int c = 0; c < width; c++)
                {
                    int o = c * 4;
                    byte a = rowBuffer[o + 3];
                    if (a < ContentAlphaThreshold) continue;
                    byte b = rowBuffer[o];
                    byte g = rowBuffer[o + 1];
                    byte r = rowBuffer[o + 2];
                    if (r < ContentDarkThreshold || g < ContentDarkThreshold || b < ContentDarkThreshold)
                    {
                        rowDarkPixels++;
                        if (rowDarkPixels >= minPixelsPerRow) break;
                    }
                }
                if (rowDarkPixels >= minPixelsPerRow)
                {
                    contentRows++;
                    if (contentRows >= minContentRows) return false;
                }
            }
            return true;
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
