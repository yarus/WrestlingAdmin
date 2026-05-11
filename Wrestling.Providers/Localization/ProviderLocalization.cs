using System;

namespace Wrestling.Providers.Localization
{
    // Minimal hook for non-UI code (Providers / domain validation) to produce
    // localized strings without taking a hard dependency on the WPF-side
    // LocalizationService. The UI layer wires `Translate` at startup; until
    // that happens, the delegate returns the fallback so unit tests and any
    // headless code paths still get readable output.
    //
    // Intentionally a static delegate rather than an interface: it's a one-way
    // function from (key, fallback) → string. An interface would add ceremony
    // (constructor injection, DI registration, mocking) for a value-typed
    // operation that's effectively a free function.
    public static class ProviderLocalization
    {
        // Replaced by App.xaml.cs at startup. Defaulting to the fallback means
        // tests and pre-init code paths get the literal Russian string and
        // remain debuggable rather than seeing an empty value.
        public static Func<string, string, string> Translate { get; set; } = (key, fallback) => fallback;

        public static string T(string key, string fallback) => Translate(key, fallback) ?? fallback;
    }
}
