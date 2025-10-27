using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace Wrestling.UI.Material.Utils.Converters
{
    public class PathToImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var imgPath = $"{AppDomain.CurrentDomain.BaseDirectory}Images\\";

            string defaultImageName;
            if (parameter != null)
            {
                defaultImageName = parameter.ToString();
            }
            else
            {
                defaultImageName = "DefaultLogo.png";
            }

            var defaultEmblem = new BitmapImage(new Uri($"{imgPath}{defaultImageName}", UriKind.Absolute));

            if (string.IsNullOrEmpty(value?.ToString())) return defaultEmblem;

            string fullFilePath = string.Empty;

            if (File.Exists(value.ToString())) {
                fullFilePath = value.ToString();
            }
            else
            {
                var fileNameItems = value.ToString().Split('\\');
                string fileName = fileNameItems[fileNameItems.Length - 1];
                fullFilePath = $"{imgPath}{fileName}";
            }

            var supportedFormats = new[]
            {
                ".png",".jpeg",".bmp",".gif"
            };

            var targetExtension = Path.GetExtension(fullFilePath).ToLower();

            return File.Exists(fullFilePath) && supportedFormats.Contains(targetExtension)
                ? new BitmapImage(new Uri(fullFilePath, UriKind.Absolute))
                : defaultEmblem;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }
    }
}