using System.Collections.Generic;
using Wrestling.Entities.Localization;

namespace Wrestling.Entities
{
    // Centralized lookup for the displayed text of a WrestlerLevelEnum. UI
    // bindings call GetDisplay() instead of pulling the enum's ToString(),
    // so a language switch flips every "Разряд" / "Level" cell. Also exposes
    // the inverse (legacy cyrillic → enum) used by EntityToInfoAdapter when
    // loading older .wrt files that stored the rank as a free-form Russian
    // string.
    public static class WrestlerLevelLabels
    {
        // Legacy Russian → enum. Old .wrt files (and any hand-edited input)
        // store these literals on WrestlerInfo.Level. We accept both the
        // canonical "I юн" / "II юн" / "III юн" forms and the slightly more
        // formal "I юн." / "II юн." spellings just in case.
        private static readonly Dictionary<string, WrestlerLevelEnum> LegacyMap =
            new Dictionary<string, WrestlerLevelEnum>(System.StringComparer.OrdinalIgnoreCase)
        {
            { "МСМК",   WrestlerLevelEnum.MSMK },
            { "МС",     WrestlerLevelEnum.MS },
            { "КМС",    WrestlerLevelEnum.KMS },
            { "I",      WrestlerLevelEnum.Adult1 },
            { "II",     WrestlerLevelEnum.Adult2 },
            { "III",    WrestlerLevelEnum.Adult3 },
            { "I юн",   WrestlerLevelEnum.Junior1 },
            { "II юн",  WrestlerLevelEnum.Junior2 },
            { "III юн", WrestlerLevelEnum.Junior3 },
            { "I юн.",  WrestlerLevelEnum.Junior1 },
            { "II юн.", WrestlerLevelEnum.Junior2 },
            { "III юн.",WrestlerLevelEnum.Junior3 },
            { "б/р",    WrestlerLevelEnum.None },
        };

        // Map raw string → enum. Tries the enum's own name first
        // ("MSMK" stored by new clients), then falls back to the legacy
        // cyrillic dictionary, then None for anything unrecognized
        // (preserves the old "no rank" semantics from empty / "б/р").
        public static WrestlerLevelEnum FromString(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return WrestlerLevelEnum.None;

            if (System.Enum.TryParse<WrestlerLevelEnum>(raw, ignoreCase: true, out var parsed))
            {
                return parsed;
            }

            if (LegacyMap.TryGetValue(raw.Trim(), out var legacy))
            {
                return legacy;
            }

            return WrestlerLevelEnum.None;
        }

        // Localized display label. Hits the JSON file via the static entity
        // hook — same fallback strings as the legacy free-form values so
        // pre-localization-init code paths still produce readable output.
        public static string GetDisplay(WrestlerLevelEnum level)
        {
            switch (level)
            {
                case WrestlerLevelEnum.MSMK:    return EntityLocalization.T("Level_MSMK", "МСМК");
                case WrestlerLevelEnum.MS:      return EntityLocalization.T("Level_MS", "МС");
                case WrestlerLevelEnum.KMS:     return EntityLocalization.T("Level_KMS", "КМС");
                case WrestlerLevelEnum.Adult1:  return EntityLocalization.T("Level_Adult1", "I");
                case WrestlerLevelEnum.Adult2:  return EntityLocalization.T("Level_Adult2", "II");
                case WrestlerLevelEnum.Adult3:  return EntityLocalization.T("Level_Adult3", "III");
                case WrestlerLevelEnum.Junior1: return EntityLocalization.T("Level_Junior1", "I юн");
                case WrestlerLevelEnum.Junior2: return EntityLocalization.T("Level_Junior2", "II юн");
                case WrestlerLevelEnum.Junior3: return EntityLocalization.T("Level_Junior3", "III юн");
                case WrestlerLevelEnum.None:
                default:                        return string.Empty;
            }
        }
    }
}
