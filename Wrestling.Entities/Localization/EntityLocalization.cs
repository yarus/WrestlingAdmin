using System;

namespace Wrestling.Entities.Localization
{
    // Minimal hook so entity-side code (achievement calculators, bracket
    // processors, MatchActionDescriber) can produce localized display strings
    // without taking a hard dependency on the WPF-side LocalizationService.
    // The UI layer wires `Translate` at startup; until that happens, the
    // delegate returns the fallback so unit tests and headless code paths
    // still get readable output.
    //
    // Same pattern as Wrestling.Providers.Localization.ProviderLocalization —
    // duplicated rather than shared because Wrestling.Entities sits below
    // Wrestling.Providers in the layering.
    public static class EntityLocalization
    {
        public static Func<string, string, string> Translate { get; set; } = (key, fallback) => fallback;

        public static string T(string key, string fallback) => Translate(key, fallback) ?? fallback;
    }
}
