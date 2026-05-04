using System.Collections.Generic;
using System.Collections.ObjectModel;
using Wrestling.Entities;

namespace Wrestling.UI.Material.Tournament.Print.PrintSchedule
{
    public class PickCarpetDialogViewModel : ObservableObject
    {
        private Carpet _selectedCarpet;

        public PickCarpetDialogViewModel(IEnumerable<Carpet> carpets)
        {
            Carpets = new ObservableCollection<Carpet>(carpets);
        }

        public ObservableCollection<Carpet> Carpets { get; }

        public Carpet SelectedCarpet
        {
            get => _selectedCarpet;
            set
            {
                _selectedCarpet = value;
                OnPropertyChanged(nameof(SelectedCarpet));
                OnPropertyChanged(nameof(CanConfirm));
            }
        }

        public bool CanConfirm => _selectedCarpet != null;
    }
}
