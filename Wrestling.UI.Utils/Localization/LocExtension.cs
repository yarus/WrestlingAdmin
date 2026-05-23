using System;
using System.Windows.Data;
using System.Windows.Markup;

namespace Wrestling.UI.Utils.Localization
{
    // Use as: <TextBlock Text="{loc:Loc Key=Settings_Title}" />
    // or shortened with the constructor positional form:
    //          <TextBlock Text="{loc:Loc Settings_Title}" />
    //
    // Produces a one-way Binding against LocalizationService.Instance's
    // string-indexer. When the active language changes the service raises
    // PropertyChanged("Item[]"), invalidating every LocExtension binding —
    // the entire UI re-evaluates without an app restart.
    [MarkupExtensionReturnType(typeof(string))]
    public class LocExtension : MarkupExtension
    {
        public LocExtension() { }

        public LocExtension(string key)
        {
            Key = key;
        }

        [ConstructorArgument("key")]
        public string Key { get; set; }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if (string.IsNullOrEmpty(Key)) return string.Empty;

            var binding = new Binding($"[{Key}]")
            {
                Source = LocalizationService.Instance,
                Mode = BindingMode.OneWay
            };

            return binding.ProvideValue(serviceProvider);
        }
    }
}
