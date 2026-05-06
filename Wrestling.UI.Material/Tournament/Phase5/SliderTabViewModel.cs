using System.Collections.Generic;
using MaterialDesignThemes.Wpf;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Material.Slider;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Tournament.Phase5
{
    // Phase 5 → Слайдер. Thin adapter: hosts the existing SliderControlViewModel
    // unchanged. Exists only so the Phase 5 wrapper can list a single
    // homogeneous IPhase5SubViewModel collection.
    public class SliderTabViewModel : ViewModelBase, IPhase5SubViewModel
    {
        private SliderControlViewModel _inner;

        public SliderTabViewModel(IDiContainer container) : base(container)
        {
        }

        public string PageName => "Слайдер";

        public PackIconKind IconKind => PackIconKind.ImageMultiple;

        public override IList<CommandButtonItem> QuickButtons => _inner?.QuickButtons;

        public SliderControlViewModel Inner => _inner;

        public override void InitData()
        {
            base.InitData();

            var nav = Resolve<INavigationService>();
            _inner = nav?.GetViewModel<SliderControlViewModel>();
            _inner?.InitData();

            OnPropertyChanged(nameof(Inner));
            OnPropertyChanged(nameof(QuickButtons));
        }
    }
}
