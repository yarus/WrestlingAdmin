using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Markup;

namespace Wrestling.UI.Utils.Localization
{
    // Singleton because XAML markup extensions need a discoverable source for
    // their indexer Bindings without DI plumbing in every UserControl. The
    // instance is also registered into DiContainer so view-model code can
    // resolve it the usual way.
    public class LocalizationService : ILocalizationService
    {
        private static readonly Lazy<LocalizationService> _instance =
            new Lazy<LocalizationService>(() => new LocalizationService());

        public static LocalizationService Instance => _instance.Value;

        private readonly Dictionary<string, IDictionary<string, string>> _byLanguage =
            new Dictionary<string, IDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        private readonly List<LanguageDescriptor> _languages = new List<LanguageDescriptor>();

        private string _currentLanguage;

        public event PropertyChangedEventHandler PropertyChanged;

        public string CurrentLanguage => _currentLanguage;

        public IReadOnlyList<LanguageDescriptor> AvailableLanguages => _languages;

        // Indexer is the binding target for {loc:Loc Key=...}. Returning the
        // key itself when missing keeps the UI debuggable — a forgotten key
        // shows up as the literal "Settings_Title" instead of an empty string.
        public string this[string key] => T(key);

        public string T(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;

            if (_currentLanguage != null && _byLanguage.TryGetValue(_currentLanguage, out var dict)
                && dict.TryGetValue(key, out var value))
            {
                return value;
            }

            return key;
        }

        public void RegisterLanguage(LanguageDescriptor descriptor, IDictionary<string, string> entries)
        {
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
            if (entries == null) throw new ArgumentNullException(nameof(entries));

            _byLanguage[descriptor.Code] = entries;

            if (!_languages.Exists(l => string.Equals(l.Code, descriptor.Code, StringComparison.OrdinalIgnoreCase)))
            {
                _languages.Add(descriptor);
            }
        }

        public bool SetLanguage(string languageCode)
        {
            if (string.IsNullOrEmpty(languageCode)) return false;
            if (!_byLanguage.ContainsKey(languageCode)) return false;
            if (string.Equals(_currentLanguage, languageCode, StringComparison.OrdinalIgnoreCase)) return true;

            _currentLanguage = languageCode;

            var descriptor = _languages.Find(l => string.Equals(l.Code, languageCode, StringComparison.OrdinalIgnoreCase));
            if (descriptor != null && !string.IsNullOrEmpty(descriptor.CultureName))
            {
                try
                {
                    var culture = new CultureInfo(descriptor.CultureName);
                    Thread.CurrentThread.CurrentCulture = culture;
                    Thread.CurrentThread.CurrentUICulture = culture;
                    CultureInfo.DefaultThreadCurrentCulture = culture;
                    CultureInfo.DefaultThreadCurrentUICulture = culture;

                    // Update the XmlLanguage on every FrameworkElement so things
                    // like text rendering and number/date formatting in bindings
                    // pick up the new culture without an app restart.
                    FrameworkElement.LanguageProperty.OverrideMetadata(
                        typeof(FrameworkElement),
                        new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(culture.IetfLanguageTag)));
                }
                catch (CultureNotFoundException)
                {
                    // Bad culture in JSON manifest — keep the old culture, the
                    // language switch itself still went through so user-facing
                    // strings update.
                }
                catch (ArgumentException)
                {
                    // OverrideMetadata can only run once per type; subsequent
                    // language switches must accept the existing XmlLanguage.
                    // Acceptable: WPF uses CurrentCulture for its formatters,
                    // which we updated above.
                }
            }

            // "Item[]" is the magic string WPF listens for to invalidate every
            // indexer binding on this source. Single notification refreshes
            // the entire UI.
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentLanguage)));
            return true;
        }
    }
}
