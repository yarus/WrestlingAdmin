using System;
using System.Collections.ObjectModel;
using System.Linq;
using Wrestling.Entities;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Utils;
using Wrestling.UI.Utils.Localization;

namespace Wrestling.UI.Material.Slider.Slides.MatBracketsSlide
{
    public class MatBracketsSlideSettingsViewModel : ViewModelBase, ISliderSettingsControl
    {
        // Auto-title format. Set at write-time so a language switch after the
        // user saves the slide does not retroactively rewrite their title.
        private static string AutoTitleFor(Mat mat)
        {
            if (mat == null) return null;
            var format = LocalizationService.Instance?.T("SlideAutoTitle_MatBrackets");
            if (string.IsNullOrEmpty(format) || format == "SlideAutoTitle_MatBrackets") format = "Сетки: {0}";
            return string.Format(format, mat.Name);
        }

        private ObservableCollection<Mat> _mats;
        private Mat _selectedMat;
        private ScreenSlide _item;

        // Auto-title: pre-fill the dialog's Title field with a sensible default
        // ("Сетки: <Ковёр>") so the user does not have to type one manually for
        // the macro slide. Same overwrite-only-if-untouched discipline as
        // GroupBracketSlideSettingsViewModel.UpdateAutoTitle.
        private bool _isInitializing;
        private string _lastAutoTitle;

        public MatBracketsSlideSettingsViewModel(IDiContainer container) : base(container)
        {
        }

        public override void InitData()
        {
            base.InitData();

            Mats = DataContext.Tournament.Mats;
        }

        public ObservableCollection<Mat> Mats
        {
            get { return _mats; }
            set
            {
                _mats = value;
                OnPropertyChanged("Mats");
            }
        }

        public Mat SelectedMat
        {
            get { return _selectedMat; }
            set
            {
                _selectedMat = value;

                _item?.SetNamedValue("MatID", _selectedMat?.ID);

                if (!_isInitializing)
                {
                    UpdateAutoTitle(AutoTitleFor(_selectedMat));
                }

                OnPropertyChanged("SelectedMat");
                OnPropertyChanged("PlannedSlidesCount");
            }
        }

        // Bound by the settings view to show the user how many slides will be
        // produced when the dialog is confirmed. Pure UX nicety; the actual
        // dedup/expansion happens in SliderControlViewModel.
        public int PlannedSlidesCount => _selectedMat?.Groups?.Count ?? 0;

        public void InitContext(ScreenSlide slide)
        {
            _isInitializing = true;
            try
            {
                InitData();

                _item = slide;

                if (slide == null)
                {
                    SelectedMat = null;
                    return;
                }

                var matID = _item.GetNamedValue("MatID");
                if (matID != null)
                {
                    var matGuid = new Guid(matID.ToString());
                    SelectedMat = DataContext.Tournament.Mats.FirstOrDefault(c => c.ID == matGuid);
                }
                else
                {
                    SelectedMat = null;
                }
            }
            finally
            {
                _lastAutoTitle = AutoTitleFor(_selectedMat);
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
