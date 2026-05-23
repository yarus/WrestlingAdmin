using System;
using System.Collections.Generic;

namespace Wrestling.UI.Material.Theme
{
    public interface IThemeManager
    {
        // Curated list of primary colors offered in Настройки → Внешний вид.
        IReadOnlyList<NamedSwatch> AvailablePrimaryColors { get; }

        bool IsDark { get; set; }
        NamedSwatch SelectedPrimary { get; set; }

        // Apply the given settings to the live Application palette. Safe to
        // call before MainWindow.Show() — uses MaterialDesignThemes
        // PaletteHelper which mutates App.Resources in place.
        void Apply(LocalUiSettings settings);

        // Raised after any successful Apply() so views that need to react
        // (printing, projector window) can override per-window theming.
        event Action ThemeChanged;
    }
}
