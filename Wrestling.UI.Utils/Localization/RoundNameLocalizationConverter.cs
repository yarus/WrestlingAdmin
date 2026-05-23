using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Data;

namespace Wrestling.UI.Utils.Localization
{
    // Display-only translator for the Russian round-name strings produced by
    // the bracket processors (see Wrestling.Entities/Bracket/*GroupBracketProcessor.cs).
    // RoundName is persisted into the .wrt JSON file as plain text — schema
    // stays in Russian. This converter does pattern-matching on the input and
    // returns a localized string at display time only.
    //
    // Unknown inputs pass through unchanged, so future processor changes /
    // legacy files / hand-edited round names all keep rendering — just without
    // translation. That tolerance is the entire point of doing this with a
    // converter instead of normalising at the processor.
    //
    // Use via {loc:LocRound RoundName} markup extension, which wires a
    // MultiBinding whose second leg listens to LocalizationService so a live
    // language switch re-runs Convert().
    public class RoundNameLocalizationConverter : IMultiValueConverter
    {
        // Russian → localization key. Two near-duplicate spellings of the same
        // concept ("3-е место" vs. "3 место") both map to the same key — the
        // processors are inconsistent and a future tidy-up shouldn't break
        // display.
        private static readonly Dictionary<string, string> ExactMap = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "Финал",      "Round_Final" },
            { "Полуфинал",  "Round_SemiFinal" },
            { "3-е место",  "Round_ThirdPlace" },
            { "3 место",    "Round_ThirdPlace" },
        };

        // "1/2 финала", "1/4 финала", "1/8 финала", ... — produced by
        // OlympicGroupBracketProcessor.GetRoundNameForRound with a Math.Pow.
        private static readonly Regex NthFinalPattern = new Regex(@"^1/(\d+) финала$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        // "Утешение Круг 0", "Утешение Круг 1", ... — consolation rounds in
        // OlympicWithConsolationFromFinalistsGroupBracketProcessor.
        private static readonly Regex ConsolationPattern = new Regex(@"^Утешение Круг (\d+)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        // "Круг 1", "Круг 2", ... — round-robin and subgroups days.
        // Check this AFTER ConsolationPattern, since the consolation strings
        // contain "Круг" as a substring.
        private static readonly Regex CirclePattern = new Regex(@"^Круг (\d+)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        // When true, applies CurrentCulture-aware ToUpper to the final string.
        // Set by the LocRoundExtension when call sites need the "ФИНАЛ" big-
        // letter rendering used on the score screen.
        public bool Upper { get; set; }

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length == 0) return string.Empty;
            var name = values[0] as string;
            if (string.IsNullOrEmpty(name)) return string.Empty;

            var translated = TranslateCore(name);
            if (Upper)
            {
                translated = translated.ToUpper(culture ?? CultureInfo.CurrentCulture);
            }
            return translated;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        private static string TranslateCore(string name)
        {
            var loc = LocalizationService.Instance;

            if (ExactMap.TryGetValue(name, out var key))
            {
                return T(loc, key, name);
            }

            // "1/N финала" — the N is preserved as-is in the formatted output.
            var m = NthFinalPattern.Match(name);
            if (m.Success)
            {
                var format = T(loc, "Round_NthFinalFormat", "1/{0} финала");
                return SafeFormat(format, m.Groups[1].Value, name);
            }

            m = ConsolationPattern.Match(name);
            if (m.Success)
            {
                var format = T(loc, "Round_ConsolationFormat", "Утешение Круг {0}");
                return SafeFormat(format, m.Groups[1].Value, name);
            }

            m = CirclePattern.Match(name);
            if (m.Success)
            {
                var format = T(loc, "Round_CircleFormat", "Круг {0}");
                return SafeFormat(format, m.Groups[1].Value, name);
            }

            // Hand-edited / future / unknown — pass through. Better to render
            // a Russian round name than blank out the display.
            return name;
        }

        private static string T(ILocalizationService loc, string key, string fallback)
        {
            if (loc == null) return fallback;
            var value = loc.T(key);
            return string.IsNullOrEmpty(value) || value == key ? fallback : value;
        }

        private static string SafeFormat(string format, string arg, string fallback)
        {
            try { return string.Format(CultureInfo.CurrentCulture, format, arg); }
            catch (FormatException) { return fallback; }
        }
    }
}
