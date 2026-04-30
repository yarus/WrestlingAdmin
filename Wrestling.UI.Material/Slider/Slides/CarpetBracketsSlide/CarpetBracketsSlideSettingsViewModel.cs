using System;
using System.Collections.ObjectModel;
using System.Linq;
using Wrestling.Entities;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Slider.Slides.CarpetBracketsSlide
{
    public class CarpetBracketsSlideSettingsViewModel : ViewModelBase, ISliderSettingsControl
    {
        private ObservableCollection<Carpet> _carpets;
        private Carpet _selectedCarpet;
        private ScreenSlide _item;

        // Auto-title: pre-fill the dialog's Title field with a sensible default
        // ("Сетки: <Ковёр>") so the user does not have to type one manually for
        // the macro slide. Same overwrite-only-if-untouched discipline as
        // GroupBracketSlideSettingsViewModel.UpdateAutoTitle.
        private bool _isInitializing;
        private string _lastAutoTitle;

        public CarpetBracketsSlideSettingsViewModel(IDiContainer container) : base(container)
        {
        }

        public override void InitData()
        {
            base.InitData();

            Carpets = DataContext.Tournament.Carpets;
        }

        public ObservableCollection<Carpet> Carpets
        {
            get { return _carpets; }
            set
            {
                _carpets = value;
                OnPropertyChanged("Carpets");
            }
        }

        public Carpet SelectedCarpet
        {
            get { return _selectedCarpet; }
            set
            {
                _selectedCarpet = value;

                _item?.SetNamedValue("CarpetID", _selectedCarpet?.ID);

                if (!_isInitializing)
                {
                    UpdateAutoTitle(_selectedCarpet != null ? "Сетки: " + _selectedCarpet.Name : null);
                }

                OnPropertyChanged("SelectedCarpet");
                OnPropertyChanged("PlannedSlidesCount");
            }
        }

        // Bound by the settings view to show the user how many slides will be
        // produced when the dialog is confirmed. Pure UX nicety; the actual
        // dedup/expansion happens in SliderControlViewModel.
        public int PlannedSlidesCount => _selectedCarpet?.Groups?.Count ?? 0;

        public void InitContext(ScreenSlide slide)
        {
            _isInitializing = true;
            try
            {
                InitData();

                _item = slide;

                if (slide == null)
                {
                    SelectedCarpet = null;
                    return;
                }

                var carpetID = _item.GetNamedValue("CarpetID");
                if (carpetID != null)
                {
                    var carpetGuid = new Guid(carpetID.ToString());
                    SelectedCarpet = DataContext.Tournament.Carpets.FirstOrDefault(c => c.ID == carpetGuid);
                }
                else
                {
                    SelectedCarpet = null;
                }
            }
            finally
            {
                _lastAutoTitle = _selectedCarpet != null ? "Сетки: " + _selectedCarpet.Name : null;
                _isInitializing = false;
            }
        }

        private void UpdateAutoTitle(string newAutoTitle)
        {
            if (_item == null || string.IsNullOrEmpty(newAutoTitle)) return;

            if (string.IsNullOrWhiteSpace(_item.Title) || _item.Title == _lastAutoTitle)
            {
                _item.Title = newAutoTitle;
                _lastAutoTitle = newAutoTitle;
            }
        }
    }
}
