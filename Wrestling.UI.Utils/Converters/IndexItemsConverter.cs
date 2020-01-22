using System;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Data;

namespace Wrestling.UI.Utils.Converters
{
    public class IndexItemsConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (values == null || values.Length < 2)
            {
                return string.Empty;
            }

            var itemsControl = values[0] as ItemsControl;
            var item = values[1];

            if (itemsControl == null || item == null)
            {
                return string.Empty;
            }

            var itemContainer = itemsControl.ItemContainerGenerator.ContainerFromItem(item);

            // It may not yet be in the collection...
            if (itemContainer == null)
            {
                return Binding.DoNothing;
            }

            var itemIndex = itemsControl.ItemContainerGenerator.IndexFromContainer(itemContainer);

            if (parameter != null)
            {
                int adjustment;

                if (int.TryParse(parameter.ToString(), out adjustment))
                {
                    itemIndex += adjustment;
                }
            }

            return itemIndex.ToString();
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
        {
            return targetTypes.Select(t => Binding.DoNothing).ToArray();
        }
    }
}