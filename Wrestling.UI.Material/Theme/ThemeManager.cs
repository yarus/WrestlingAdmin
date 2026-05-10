using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows.Media;
using MaterialDesignColors;
using MaterialDesignThemes.Wpf;
using Wrestling.Entities;

namespace Wrestling.UI.Material.Theme
{
    public class ThemeManager : ObservableObject, IThemeManager
    {
        private readonly ILocalUiSettingsStorage _storage;
        private readonly PaletteHelper _paletteHelper = new PaletteHelper();

        private bool _isDark;
        private NamedSwatch _selectedPrimary;
        private string _secondaryColorId = "Lime";

        // Suppresses the persist-on-change side effect while Apply() is
        // populating IsDark / SelectedPrimary from a freshly-loaded settings
        // file. Without this, the initial setter calls would each trigger an
        // unnecessary save and palette-swap.
        private bool _suppressPersist;

        public ThemeManager(ILocalUiSettingsStorage storage)
        {
            _storage = storage;

            AvailablePrimaryColors = BuildCuratedPalette();
        }

        public IReadOnlyList<NamedSwatch> AvailablePrimaryColors { get; }

        public bool IsDark
        {
            get => _isDark;
            set
            {
                if (_isDark == value) return;
                _isDark = value;
                OnPropertyChanged(nameof(IsDark));
                ApplyAndPersist();
            }
        }

        public NamedSwatch SelectedPrimary
        {
            get => _selectedPrimary;
            set
            {
                if (ReferenceEquals(_selectedPrimary, value)) return;
                _selectedPrimary = value;
                OnPropertyChanged(nameof(SelectedPrimary));
                ApplyAndPersist();
            }
        }

        public event Action ThemeChanged;

        public void Apply(LocalUiSettings settings)
        {
            if (settings == null) return;

            _suppressPersist = true;
            try
            {
                _isDark = string.Equals(settings.BaseTheme, "Dark", StringComparison.OrdinalIgnoreCase);
                _selectedPrimary = ResolvePrimary(settings.PrimaryColor);
                _secondaryColorId = string.IsNullOrWhiteSpace(settings.SecondaryColor) ? "Lime" : settings.SecondaryColor;

                OnPropertyChanged(nameof(IsDark));
                OnPropertyChanged(nameof(SelectedPrimary));

                ApplyToPalette();
            }
            finally
            {
                _suppressPersist = false;
            }
        }

        private void ApplyAndPersist()
        {
            if (_suppressPersist) return;
            ApplyToPalette();
            _storage.Save(Snapshot());
        }

        private void ApplyToPalette()
        {
            try
            {
                var theme = _paletteHelper.GetTheme();
                theme.SetBaseTheme(_isDark ? BaseTheme.Dark : BaseTheme.Light);

                if (_selectedPrimary != null)
                {
                    theme.SetPrimaryColor(_selectedPrimary.Color);
                }

                var secondaryColor = LookupSwatchColor(_secondaryColorId);
                if (secondaryColor.HasValue)
                {
                    theme.SetSecondaryColor(secondaryColor.Value);
                }

                _paletteHelper.SetTheme(theme);
                ThemeChanged?.Invoke();
            }
            catch (Exception ex)
            {
                // PaletteHelper.SetTheme can throw if the App resource tree
                // hasn't finished loading. The app keeps the prior palette on
                // failure instead of crashing — a misconfigured prefs file
                // must not make the app unlaunchable.
                Debug.WriteLine($"ThemeManager.ApplyToPalette failed: {ex}");
            }
        }

        private LocalUiSettings Snapshot()
        {
            return new LocalUiSettings
            {
                BaseTheme = _isDark ? "Dark" : "Light",
                PrimaryColor = _selectedPrimary?.Id ?? "DeepPurple",
                SecondaryColor = _secondaryColorId
            };
        }

        private NamedSwatch ResolvePrimary(string id)
        {
            if (!string.IsNullOrWhiteSpace(id))
            {
                var hit = AvailablePrimaryColors.FirstOrDefault(c =>
                    string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase));
                if (hit != null) return hit;
            }
            return AvailablePrimaryColors.FirstOrDefault(c => c.Id == "DeepPurple")
                   ?? AvailablePrimaryColors[0];
        }

        // SwatchHelper.Lookup is keyed on MaterialDesignColor — the enum has
        // a shorthand entry per swatch family (e.g. `DeepPurple`, equivalent
        // to `DeepPurple500`). Returns null if the name isn't a recognized
        // swatch — caller falls back to its own default.
        private static Color? LookupSwatchColor(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            if (!Enum.TryParse<MaterialDesignColor>(id, ignoreCase: true, out var parsed)) return null;
            return SwatchHelper.Lookup.TryGetValue(parsed, out var color) ? color : (Color?)null;
        }

        private static IReadOnlyList<NamedSwatch> BuildCuratedPalette()
        {
            // Curated picker. Entries mix MaterialDesignColor enum names
            // (resolved via SwatchHelper) with explicit Color literals for
            // shades MD doesn't expose by short name (off-black, mid-grey).
            // MD3 auto-derives OnPrimary text contrast from the primary's
            // luminance, so dark primaries get light text and vice-versa
            // without an explicit ColorPair.
            //
            // Off-black uses #212121 (Grey900) rather than pure #000000:
            // MD3 generates the primary-container tonal step from primary,
            // and pure black collapses container/surface variants into a
            // single indistinguishable shade. Grey900 preserves the tonal
            // headroom while reading as black to the eye.
            var picks = new (string Id, string Label, Color? ExplicitColor)[]
            {
                ("DeepPurple", "Тёмно-фиолетовый", null),
                ("Purple",     "Фиолетовый",       null),
                ("Indigo",     "Индиго",           null),
                ("Blue",       "Синий",            null),
                ("Teal",       "Бирюзовый",        null),
                ("Green",      "Зелёный",          null),
                ("Amber",      "Янтарный",         null),
                ("Orange",     "Оранжевый",        null),
                ("DeepOrange", "Тёмно-оранжевый",  null),
                ("Red",        "Красный",          null),
                ("Pink",       "Розовый",          null),
                ("BlueGrey",   "Серо-синий",       null),
                ("Grey",       "Серый",            Color.FromRgb(0x75, 0x75, 0x75)),
                ("Black",      "Чёрный",           Color.FromRgb(0x21, 0x21, 0x21))
            };

            var result = new List<NamedSwatch>(picks.Length);
            foreach (var (id, label, explicitColor) in picks)
            {
                var color = explicitColor ?? LookupSwatchColor(id);
                if (!color.HasValue) continue;
                result.Add(new NamedSwatch(id, label, color.Value));
            }
            return new ReadOnlyCollection<NamedSwatch>(result);
        }
    }
}
