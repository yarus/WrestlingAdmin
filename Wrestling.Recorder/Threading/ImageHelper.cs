using System;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Windows.Media.Imaging;
using System.Windows;
using System.Windows.Interop;

namespace Uniso.Helpers.Windows
{
    public static class ImageHelper
    {
        public static BitmapSource ToImageSource(this Bitmap bitmap)
        {
            var ptr = bitmap.GetHbitmap();
            BitmapSource bs;

            try
            {
                bs = Imaging.CreateBitmapSourceFromHBitmap(
                    ptr,
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
            }
            finally
            {
                DeleteObject(ptr);
            }

            bs.Freeze();
            return bs;
        }

        [DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);
    }
}
