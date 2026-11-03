using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace Wrestling.UI.Material.Utils.Converters
{
    public class PathToImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return TryLoadImage(value?.ToString()) ?? TryLoadImage(parameter?.ToString() ?? "DefaultLogo.png");
        }

        private static BitmapImage TryLoadImage(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            try
            {
                if (!File.Exists(path))
                    path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images", Path.GetFileName(path));
                if (!File.Exists(path) || !new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif" }
                    .Contains(Path.GetExtension(path).ToLowerInvariant())) return null;

                // External emblems may disappear, be unreadable or contain a corrupt image.
                // Load completely now so the file can be replaced and decoder errors use the fallback.
                using var stream = File.OpenRead(path);
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = stream;
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException
                or ArgumentException or NotSupportedException or FormatException or COMException)
            {
                return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }
    }
}
