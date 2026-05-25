using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Wrestling.Entities;
using Wrestling.UI.Utils.Localization;

namespace Wrestling.UI.Material.Tournament.Standing.Mats
{
    public partial class MatsView : UserControl
    {
        public MatsView()
        {
            InitializeComponent();
        }

        // Move-to-Part trigger button — opens an ad-hoc ContextMenu under the
        // button with the parts list filtered to exclude the group's current
        // part. Items wire directly to MoveGroupToPartCommand so the existing
        // VM flow (snackbar + autosave + peer-sync) runs unchanged. Going
        // via ContextMenu instead of PopupBox lets the trigger use the same
        // MaterialDesignFloatingActionMiniButton style as the neighbouring
        // ↑ ↓ ✕ buttons.
        private void OnMoveToPartTriggerClick(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            var group = btn?.DataContext as AgeWeightGroup;
            var vm = DataContext as MatsViewModel;
            if (btn == null || group == null || vm?.Parts == null) return;

            var menu = new ContextMenu
            {
                Placement = PlacementMode.Bottom,
                PlacementTarget = btn,
                StaysOpen = false
            };

            var header = new MenuItem
            {
                Header = LocalizationService.Instance.T("Mats_MoveToPart_Header") ?? "Перенести в часть",
                IsEnabled = false,
                FontWeight = FontWeights.SemiBold
            };
            menu.Items.Add(header);
            menu.Items.Add(new Separator());

            foreach (var part in vm.Parts.Where(p => p?.ID != group.PartID))
            {
                var captured = part;
                var item = new MenuItem { Header = captured.Name };
                item.Click += (s, args) =>
                {
                    var pkg = Tuple.Create(group, captured);
                    if (vm.MoveGroupToPartCommand?.CanExecute(pkg) == true)
                    {
                        vm.MoveGroupToPartCommand.Execute(pkg);
                    }
                };
                menu.Items.Add(item);
            }

            menu.IsOpen = true;
        }

        // "+ Группу" inside a (Part, Mat) cell in the multi-part layout.
        // The button's DataContext is the MatsPartMatPanelVm — we extract
        // (Mat, Part) and run the VM's BindGroupForPartCommand.
        private void OnAddGroupForPartMatClick(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            var panel = btn?.DataContext as MatsPartMatPanelVm;
            if (panel == null) return;

            var vm = DataContext as MatsViewModel;
            if (vm?.BindGroupForPartCommand == null) return;

            var pkg = Tuple.Create(panel.Mat, panel.Part);
            if (vm.BindGroupForPartCommand.CanExecute(pkg))
            {
                vm.BindGroupForPartCommand.Execute(pkg);
            }
        }

    }
}
