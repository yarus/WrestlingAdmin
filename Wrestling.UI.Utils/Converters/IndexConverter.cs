using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;

namespace Wrestling.UI.Utils.Converters
{
    public class IndexConverter : IValueConverter
    {
        public object Convert(object value, Type TargetType, object parameter, CultureInfo culture)
        {
            if (value == null) return string.Empty;

            ListViewItem item = (ListViewItem)value;

            ListView listView = ItemsControl.ItemsControlFromItemContainer(item) as ListView;

            if (listView == null) return string.Empty;

            int index = listView.ItemContainerGenerator.IndexFromContainer(item);

            if (parameter != null)
            {
                if (int.TryParse(parameter.ToString(), out var adjustment))
                {
                    index += adjustment;
                }
            }

            return index.ToString();
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return string.Empty;
        }
    }
}