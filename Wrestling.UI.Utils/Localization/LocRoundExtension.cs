using System;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace Wrestling.UI.Utils.Localization
{
    // Use as: <TextBlock Text="{loc:LocRound RoundName}" />
    // or with uppercasing: <TextBlock Text="{loc:LocRound RoundName, Upper=True}" />
    //
    // Internally builds a MultiBinding whose first leg watches the source's
    // RoundName property, second leg listens to LocalizationService so the
    // language switch re-fires the converter and updates the displayed text.
    [MarkupExtensionReturnType(typeof(string))]
    public class LocRoundExtension : MarkupExtension
    {
        public LocRoundExtension() { }

        public LocRoundExtension(PropertyPath path)
        {
            Path = path;
        }

        [ConstructorArgument("path")]
        public PropertyPath Path { get; set; }

        // Optional uppercase pass. The score-screen call sites currently
        // chain {Binding RoundName, Converter=UpperCaseConverter} — those
        // become {loc:LocRound RoundName, Upper=True}.
        public bool Upper { get; set; }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if (Path == null) return string.Empty;

            var multi = new MultiBinding
            {
                Mode = BindingMode.OneWay,
                Converter = new RoundNameLocalizationConverter { Upper = Upper }
            };

            multi.Bindings.Add(new Binding { Path = Path, Mode = BindingMode.OneWay });

            // Indexer leg: any string key produces a binding that re-fires on
            // PropertyChanged("Item[]") which the service raises on language
            // switch. The exact key doesn't matter for the conversion — the
            // converter ignores values[1] — it's a refresh ping only.
            multi.Bindings.Add(new Binding("[__round_ping]")
            {
                Source = LocalizationService.Instance,
                Mode = BindingMode.OneWay
            });

            return multi.ProvideValue(serviceProvider);
        }
    }
}
