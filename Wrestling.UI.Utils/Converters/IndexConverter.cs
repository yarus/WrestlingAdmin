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
            ListViewItem item = (ListViewItem)value;
            ListView listView = ItemsControl.ItemsControlFromItemContainer(item) as ListView;

            if (listView == null) return string.Empty;

            int index = listView.ItemContainerGenerator.IndexFromContainer(item);

            if (parameter != null)
            {
                int adjustment;

                if (int.TryParse(parameter.ToString(), out adjustment))
                {
                    index += adjustment;
                }
            }

            return index.ToString();
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}