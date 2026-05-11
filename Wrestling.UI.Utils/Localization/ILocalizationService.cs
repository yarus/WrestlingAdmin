using System.Collections.Generic;
using System.ComponentModel;

namespace Wrestling.UI.Utils.Localization
{
    public interface ILocalizationService : INotifyPropertyChanged
    {
        string CurrentLanguage { get; }

        IReadOnlyList<LanguageDescriptor> AvailableLanguages { get; }

        string this[string key] { get; }

        string T(string key);

        void RegisterLanguage(LanguageDescriptor descriptor, IDictionary<string, string> entries);

        bool SetLanguage(string languageCode);
    }

    public class LanguageDescriptor
    {
        public LanguageDescriptor(string code, string displayName, string cultureName)
        {
            Code = code;
            DisplayName = displayName;
            CultureName = cultureName;
        }

        public string Code { get; }
        public string DisplayName { get; }
        public string CultureName { get; }
    }
}
