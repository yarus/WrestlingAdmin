using System;
using System.IO;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Wrestling.UI.Utils
{
    /// <summary>
    /// A print mechanism that prints a large visual as bitmaps across
    /// multiple pages. Adapted from https://www.codeproject.com/Articles/339416/Printing-large-WPF-UserControls
    /// under the Code Project Open License (CPOL): https://www.codeproject.com/info/cpol10.aspx
    /// </summary>
    public class VisualPrinter
    {
        #region Members
        /// <summary>
        /// The left and right-hand margins in pixels.
        /// </summary>
        public static int horzBorder;

        /// <summary>
        /// The top and bottom margins in pixels.
        /// </summary>
        public static int vertBorder;

        /// <summary>
        /// The expected horizontal DPI.
        /// </summary>
        private static double dpiX;

        /// <summary>
        /// The expected vertical DPI.
        /// </summary>
        private static double dpiY;
        #endregion

        #region Static Constructor
        /// <summary>
        /// Sets default member values.
        /// </summary>
        static VisualPrinter()
        {
            horzBorder = 48;
            vertBorder = 96;
            dpiX = 300;
            dpiY = 300;
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Prints a visual, breaking across pages. The user should've already
        /// accepted the print job. Returns success.
        /// </summary>
        public static bool PrintAcrossPages(PrintDialog dlg, FrameworkElement element, string documentName)
        {
            FrameworkElement printable = element;
            System.Drawing.Bitmap bmp = null;

            if (dlg != null && printable != null)
            {
                Mouse.OverrideCursor = Cursors.Wait;

                PrintCapabilities capabilities =
                    dlg.PrintQueue.GetPrintCapabilities(dlg.PrintTicket);
                dlg.PrintTicket.PageBorderless = System.Printing.PageBorderless.None;

                double dpiScale = dpiY / 96.0;
                FixedDocument document = new FixedDocument();

                bool error = false;

                try
                {
                    //Sets width and waits for changes to settle.
                    printable.Width = capabilities.PageImageableArea.ExtentWidth;
                    printable.UpdateLayout();

                    //Recomputes the desired height.
                    printable.Measure(new Size(
                        double.PositiveInfinity,
                        double.PositiveInfinity));

                    //Sets the new desired size.
                    Size size = new Size(
                        capabilities.PageImageableArea.ExtentWidth,
                        printable.DesiredSize.Height);

                    //Measures and arranges to the desired size.
                    printable.Measure(size);
                    printable.Arrange(new Rect(size));

                    //Converts GUI to bitmap at 300 DPI
                    RenderTargetBitmap bmpTarget = new RenderTargetBitmap(
                        (int)(capabilities.PageImageableArea.ExtentWidth * dpiScale),
                        (int)(printable.ActualHeight * dpiScale),
                        dpiX, dpiY, PixelFormats.Pbgra32);
                    bmpTarget.Render(printable);

                    //Converts RenderTargetBitmap to bitmap.
                    PngBitmapEncoder png = new PngBitmapEncoder();
                    png.Frames.Add(BitmapFrame.Create(bmpTarget));

                    using (MemoryStream memStream = new MemoryStream())
                    {
                        png.Save(memStream);
                        bmp = new System.Drawing.Bitmap(memStream);
                        png = null;
                    }

                    using (bmp)
                    {
                        document.DocumentPaginator.PageSize =
                            new Size(dlg.PrintableAreaWidth, dlg.PrintableAreaHeight);

                        //Breaks bitmap to fit across pages.
                        int pageBreak = 0;
                        int lastPageBreak = 0;
                        int pageHeight = (int)
                            (capabilities.PageImageableArea.ExtentHeight * dpiScale);

                        //Adds each full page.
                        while (pageBreak < bmp.Height - pageHeight)
                        {
                            pageBreak += pageHeight;

                            //Finds a page breakpoint from bottom, up.
                            pageBreak = FindRowBreakpoint(bmp, lastPageBreak, pageBreak);

                            //Adds the image segment to its own page.
                            PageContent pageContent = GeneratePageContent(
                                bmp, lastPageBreak, pageBreak,
                                document.DocumentPaginator.PageSize.Width,
                                document.DocumentPaginator.PageSize.Height,
                                capabilities);

                            document.Pages.Add(pageContent);
                            lastPageBreak = pageBreak;
                        }

                        //Adds remaining page contents.
                        PageContent lastPageContent = GeneratePageContent(
                            bmp, lastPageBreak,
                            bmp.Height, document.DocumentPaginator.PageSize.Width,
                            document.DocumentPaginator.PageSize.Height, capabilities);

                        document.Pages.Add(lastPageContent);
                    }
                }
                catch (Exception e)
                {
                    error = true;
                    Console.Write(e);
                }

                //Drops visual size adjustments.
                finally
                {
                    //Unsets width and waits for changes to settle.
                    printable.Width = double.NaN;
                    printable.UpdateLayout();

                    printable.LayoutTransform = new ScaleTransform(1, 1);

                    //Recomputes the desired height.
                    Size size = new Size(
                        capabilities.PageImageableArea.ExtentWidth,
                        capabilities.PageImageableArea.ExtentHeight);

                    //Measures and arranges to the desired size.
                    printable.Measure(size);
                    printable.Arrange(new Rect(new Point(
                        capabilities.PageImageableArea.OriginWidth,
                        capabilities.PageImageableArea.OriginHeight), size));

                    Mouse.OverrideCursor = null;
                }

                if (!error)
                {
                    dlg.PrintDocument(document.DocumentPaginator, documentName);
                    return true;
                }

                return false;
            }

            return false;
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// Iterates from the bottom line upwards to the top (so as to trim as
        /// little as possible from the complete page) to determine where to
        /// separate a page. Returns the row to break, or last if none found.
        /// </summary>
        private static unsafe int FindRowBreakpoint(
            System.Drawing.Bitmap bmp,
            int topLine,
            int bottomLine)
        {
            //Any computed deviation above the threshold
            //is considered too detailed to break on.
            double deviationThreshold = 1627500;

            //Locks to read data.
            System.Drawing.Imaging.BitmapData bmpData = bmp.LockBits(
                new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height),
                System.Drawing.Imaging.ImageLockMode.ReadOnly, bmp.PixelFormat);

            //Sets the initial row and position.
            int stride = bmpData.Stride;
            IntPtr topLeftPixel = bmpData.Scan0;
            byte* p = (byte*)(void*)topLeftPixel;

            //Iterates from bottom to top to find a breakable row.
            for (int i = bottomLine; i > topLine; i--)
            {
                int count = 0;
                double total = 0;
                double totalVariance = 0;

                //Sets pointer to this row.
                p = (byte*)(void*)topLeftPixel + stride * i;

                //Iterates through each consecutive pixel in the given row.
                for (int column = 0; column < bmp.Width; column++)
                {
                    count++;

                    byte red = p[1];
                    byte green = p[2];
                    byte blue = p[3];

                    //Faster than System.Drawing.Color.FromArgb(0, red, green, blue).ToArgb().
                    int pixelValue = (red << 16) | (green << 8) | blue;

                    total += pixelValue;
                    double average = total / count;
                    totalVariance += (pixelValue - average) * (pixelValue - average);

                    //Skips to next pixel.
                    p += 4;
                }

                //Breaks on this line if possible.
                double standardDeviation = Math.Sqrt(totalVariance / count);
                if (Math.Sqrt(totalVariance / count) < deviationThreshold)
                {
                    bmp.UnlockBits(bmpData);
                    return i;
                }
            }

            //Breaks on the last line given if no break row is found.
            bmp.UnlockBits(bmpData);
            return bottomLine;
        }

        /// <summary>
        /// Sizes the given bitmap to the page size and returns it as part
        /// of a printable page.
        /// </summary>
        private static PageContent GeneratePageContent(
            System.Drawing.Bitmap bmp,
            int top,
            int bottom,
            double pageWidth,
            double PageHeight,
            System.Printing.PrintCapabilities capabilities)
        {
            Image pageImage;
            BitmapSource bmpSource;

            //Creates a page with a specific width/height.
            FixedPage printPage = new FixedPage();
            printPage.Width = pageWidth;
            printPage.Height = PageHeight;

            //Cuts the given image at a reasonable boundary.
            int newImageHeight = bottom - top;

            //Creates a clone of the image.
            using (System.Drawing.Bitmap bmpCut =
                bmp.Clone(new System.Drawing.Rectangle(0, top, bmp.Width, newImageHeight),
                    bmp.PixelFormat))
            {
                //Prepares the bitmap source.
                pageImage = new Image();
                bmpSource =
                    System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                        bmpCut.GetHbitmap(),
                        IntPtr.Zero,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromWidthAndHeight(bmpCut.Width, bmpCut.Height));
            }

            //Adds the bitmap to the page.
            pageImage.Source = bmpSource;
            pageImage.VerticalAlignment = VerticalAlignment.Top;
            printPage.Children.Add(pageImage);

            PageContent pageContent = new PageContent();
            ((System.Windows.Markup.IAddChild)pageContent).AddChild(printPage);

            //Adds a margin.
            printPage.Margin = new Thickness(
                horzBorder, vertBorder,
                horzBorder, vertBorder);

            FixedPage.SetLeft(pageImage, capabilities.PageImageableArea.OriginWidth);
            FixedPage.SetTop(pageImage, capabilities.PageImageableArea.OriginHeight);

            //Adjusts for the margins and to fit the page.
            pageImage.Width = capabilities.PageImageableArea.ExtentWidth - horzBorder * 2;
            pageImage.Height = capabilities.PageImageableArea.ExtentHeight - vertBorder * 2;
            return pageContent;
        }
        #endregion
    }
}
