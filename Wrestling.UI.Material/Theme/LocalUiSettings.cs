namespace Wrestling.UI.Material.Theme
{
    // Per-machine UI preferences. Lives in %LocalAppData%/WrestlingAdmin/
    // local_ui_settings.json, kept separate from .wrt tournament data so the
    // operator's chosen theme does not flip when opening someone else's
    // tournament file. String fields hold MaterialDesignColors enum names
    // (e.g. "DeepPurple", "Lime") and "Light"/"Dark" — kept as strings to
    // stay forward-compatible if MaterialDesignThemes adds new entries.
    public class LocalUiSettings
    {
        public LocalUiSettings()
        {
            // Defaults match the historical hardcoded BundledTheme so a
            // first launch (or a missing/corrupt prefs file) is visually
            // identical to the pre-feature behavior.
            BaseTheme = "Light";
            PrimaryColor = "DeepPurple";
            SecondaryColor = "Lime";
            LanguageCode = "ru";
        }

        public string BaseTheme { get; set; }
        public string PrimaryColor { get; set; }
        public string SecondaryColor { get; set; }

        // ISO 639-1 language code (e.g. "ru", "en"). Resolved at startup
        // against LocalizationService.AvailableLanguages — unknown values
        // fall back to the first registered language.
        public string LanguageCode { get; set; }
    }
}
