using System.Collections.Generic;

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
            LanguageCode = null;
            RecentTournamentFiles = new List<string>();
        }

        public string BaseTheme { get; set; }
        public string PrimaryColor { get; set; }
        public string SecondaryColor { get; set; }

        // ISO 639-1 language code (e.g. "ru", "en"). Null/empty means the
        // operator has never picked a language — startup auto-detects from
        // the OS UI culture, then falls back to English, then to the first
        // registered language. Saving from Settings writes an explicit code.
        public string LanguageCode { get; set; }

        // Most-recent-first list of full .wrt paths the operator opened or
        // created on this machine. Capped + deduped + pruned by
        // RecentTournamentsService — the DTO itself is just a flat string
        // bag. Newtonsoft uses the parameterless ctor before overlay, so
        // legacy local_ui_settings.json files (without this field) load as
        // an empty list.
        public List<string> RecentTournamentFiles { get; set; }
    }
}
