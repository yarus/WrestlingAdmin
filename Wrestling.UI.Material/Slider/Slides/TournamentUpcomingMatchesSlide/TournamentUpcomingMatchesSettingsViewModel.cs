using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using MvvmDialogs.FrameworkDialogs.OpenFile;
using Wrestling.Entities;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Material.Tournament;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Slider.Slides.TournamentUpcomingMatchesSlide
{
    public sealed class TournamentUpcomingMatchesSettingsViewModel : ViewModelBase, ISliderSettingsControl
    {
        private ScreenSlide _slide;
        private int _count;
        private string _background;
        private int _opacity;
        private ICommand _selectAllCommand;
        private ICommand _setBackgroundCommand;

        public TournamentUpcomingMatchesSettingsViewModel(IDiContainer di) : base(di) { }
        public ObservableCollection<UpcomingMatSelection> Mats { get; } = new ObservableCollection<UpcomingMatSelection>();

        public int ShowMatchesCount
        {
            get => _count;
            set
            {
                _count = Math.Max(1, value);
                _slide?.SetNamedValue("ShowMatchesCount", _count);
                OnPropertyChanged(nameof(ShowMatchesCount));
            }
        }

        public string SliderBackgroundImagePath
        {
            get => _background;
            set { _background = value; _slide?.SetNamedValue("SliderBackgroundImagePath", value); OnPropertyChanged(nameof(SliderBackgroundImagePath)); }
        }

        public int SliderOpacityValue
        {
            get => _opacity;
            set { _opacity = Math.Clamp(value, 0, 100); _slide?.SetNamedValue("SliderOpacityValue", _opacity); OnPropertyChanged(nameof(SliderOpacityValue)); }
        }

        public ICommand SelectAllCommand => _selectAllCommand ??= new RelayCommand(p =>
        {
            foreach (var mat in Mats) mat.IsSelected = true;
        }, p => Mats.Count > 0);

        public ICommand SetSliderBackgroundCommand => _setBackgroundCommand ??= new RelayCommand(p =>
        {
            var settings = new OpenFileDialogSettings
            {
                Title = TournamentViewModelBase.T("OpenImage_DialogTitle", "Открыть файл с изображением"),
                Filter = TournamentViewModelBase.T("ImageFilter", "Изображения (*.jpg)|*.jpg|All Files (*.*)|*.*")
            };
            if (Dialog.ShowOpenFileDialog(this, settings) == true) SliderBackgroundImagePath = settings.FileName;
        }, p => true);

        public void InitContext(ScreenSlide slide)
        {
            InitData();
            _slide = slide;
            Mats.Clear();
            if (slide == null) return;

            var ids = TournamentUpcomingMatchesViewModel.ReadMatIds(slide);
            var all = slide.GetNamedValue("MatIDs") == null;
            foreach (var mat in DataContext.Tournament.Mats)
                Mats.Add(new UpcomingMatSelection(mat, all || (mat.ID.HasValue && ids.Contains(mat.ID.Value)), this));
            if (Mats.Count > 0 && !Mats.Any(m => m.IsSelected)) Mats[0].IsSelected = true;
            SaveSelection();
            ShowMatchesCount = Convert.ToInt32(slide.GetNamedValue("ShowMatchesCount") ?? 4);
            SliderBackgroundImagePath = slide.GetNamedValue("SliderBackgroundImagePath")?.ToString()
                ?? DataContext.Tournament.Settings.SliderBackgroundImagePath;
            SliderOpacityValue = Convert.ToInt32(slide.GetNamedValue("SliderOpacityValue")
                ?? DataContext.Tournament.Settings.SliderOpacityValue);
            if (string.IsNullOrWhiteSpace(slide.Title)) slide.Title = TournamentUpcomingMatchesSlide.GetTitle();
        }

        internal void SaveSelection() => _slide?.SetNamedValue("MatIDs",
            string.Join(",", Mats.Where(m => m.IsSelected && m.Mat.ID.HasValue).Select(m => m.Mat.ID.Value)));

        public sealed class UpcomingMatSelection : ObservableObject
        {
            private bool _selected;
            private readonly TournamentUpcomingMatchesSettingsViewModel _owner;
            public UpcomingMatSelection(Mat mat, bool selected, TournamentUpcomingMatchesSettingsViewModel owner)
            { Mat = mat; _selected = selected; _owner = owner; }
            public Mat Mat { get; }
            public bool IsSelected
            {
                get => _selected;
                set
                {
                    if (_selected == value) return;
                    if (value || _owner.Mats.Count(m => m.IsSelected) > 1)
                    {
                        _selected = value;
                        _owner.SaveSelection();
                    }
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }
    }
}
