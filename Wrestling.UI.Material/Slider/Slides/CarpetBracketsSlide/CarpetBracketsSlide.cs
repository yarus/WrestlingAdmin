using System;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Slider.Slides.CarpetBracketsSlide
{
    // "Macro" slide type: never persisted into a channel. The AddSlide flow in
    // SliderControlViewModel detects this SlideType after the dialog closes and
    // expands it into one regular GroupBracketSlide per carpet group. If a slide
    // of this type ever reaches the rendering pipeline it means the expansion
    // step was skipped — fail loudly rather than show an empty slide.
    public class CarpetBracketsSlide : ISlideType
    {
        public const string TypeName = "Сетки ковра";

        public CarpetBracketsSlide(IDiContainer di)
        {
            SettingsControl = di.Resolve<CarpetBracketsSlideSettingsViewModel>();
        }

        public string SlideType => TypeName;
        public ISliderSettingsControl SettingsControl { get; }

        public ISliderViewControl CreateViewControl()
        {
            throw new NotSupportedException(
                "CarpetBracketsSlide is a macro and must be expanded by SliderControlViewModel.AddSlide before it can be rendered.");
        }
    }
}
