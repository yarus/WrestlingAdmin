using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MaterialDesignThemes.Wpf;
using Wrestling.Entities;

namespace Wrestling.UI.Material.Tournament.Print.PrintSchedule
{
    // Prompts the user to pick a carpet from the given list. When only one
    // carpet is available, returns it directly without showing a dialog.
    // Returns null when the user cancels or the list is empty.
    public static class CarpetPicker
    {
        public static async Task<Carpet> PickAsync(IEnumerable<Carpet> carpets)
        {
            var list = carpets?.ToList();
            if (list == null || list.Count == 0) return null;
            if (list.Count == 1) return list[0];

            var vm = new PickCarpetDialogViewModel(list);
            var dialog = new PickCarpetDialog { DataContext = vm };

            var result = await DialogHost.Show(dialog, "RootDialog");
            return (result is bool confirmed && confirmed) ? vm.SelectedCarpet : null;
        }
    }
}
