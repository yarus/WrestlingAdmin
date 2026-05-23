using System;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Slider.Slides.MatBracketsSlide
{
    // "Macro" slide type: never persisted into a channel. The AddSlide flow in
    // SliderControlViewModel detects this SlideType after the dialog closes and
    // expands it into one regular GroupBracketSlide per mat group. If a slide
    // of this type ever reaches the rendering pipeline it means the expansion
    // step was skipped — fail loudly rather than show an empty slide.
    public class MatBracketsSlide : ISlideType
    {
        // Stable identity sentinel for the macro-detection logic in
        // SliderControlViewModel.AddSlide. Must NOT be localized — code compares
        // SlideType against this const to decide whether to expand the macro.
        public const string TypeName = "Сетки ковра";

        public MatBracketsSlide(IDiContainer di)
        {
            SettingsControl = di.Resolve<MatBracketsSlideSettingsViewModel>();
        }

        // Returning TypeName as-is keeps SliderControlViewModel.AddSlide's
        // identity comparison (vm.Item.SlideType == MatBracketsSlide.TypeName)
        // working regardless of UI language. The label that shows in the
        // type-picker stays Russian — switching it would require routing the
        // macro detection through a reference check first. Accepted as a small
        // localization gap on the one macro-type entry.
        public string SlideType => TypeName;
        public ISliderSettingsControl SettingsControl { get; }

        public ISliderViewControl CreateViewControl()
        {
            throw new NotSupportedException(
                "MatBracketsSlide is a macro and must be expanded by SliderControlViewModel.AddSlide before it can be rendered.");
        }
    }
}
