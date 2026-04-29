using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace Wrestling.UI.Material.Utils.Converters
{
    // Converts a file path to a BitmapImage, or null if the path is empty /
    // the file is missing / the format is unsupported. Unlike PathToImageConverter
    // (which falls back to DefaultLogo.png), this one returns null so an
    // Image control bound through it renders nothing.
    //
    // Accepts both an absolute path and a relative filename. Relative names
    // are resolved against the app's Images/ folder (same convention as
    // PathToImageConverter / EmblemPath), so a value persisted as just
    // "stamp.png" round-trips through .wrt save/load without absolute-path
    // drift between machines or user-folder layouts.
    //
    // Used for optional decorations like the stamp+signatures overlay in
    // print protocols, where "no image configured" must mean "no overlay"
    // — not "show a default placeholder".
    //
    // BitmapCacheOption.OnLoad is critical: without it, the underlying file
    // stays open until the BitmapImage is GC'd, which prevents the user
    // from re-saving over the file or moving it.
    public class OptionalImagePathConverter : IValueConverter
    {
        private static readonly string[] SupportedExtensions =
        {
            ".png", ".jpg", ".jpeg", ".bmp", ".gif"
        };

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var raw = value?.ToString();
            if (string.IsNullOrWhiteSpace(raw)) return null;

            string fullPath;
            if (File.Exists(raw))
            {
                fullPath = raw;
            }
            else
            {
                var imagesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images");
                fullPath = Path.Combine(imagesDir, Path.GetFileName(raw));
                if (!File.Exists(fullPath)) return null;
            }

            var ext = Path.GetExtension(fullPath).ToLowerInvariant();
            if (!SupportedExtensions.Contains(ext)) return null;

            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(fullPath, UriKind.Absolute);
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch
            {
                return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
