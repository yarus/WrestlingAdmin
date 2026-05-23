using System.Windows.Media;

namespace Wrestling.UI.Material.Theme
{
    // View-model for one entry in the primary-color picker. Binds to a small
    // round Border whose Fill = Color, label below = DisplayName, with the
    // currently-selected swatch decorated by the parent template.
    public class NamedSwatch
    {
        public NamedSwatch(string id, string displayName, Color color)
        {
            Id = id;
            DisplayName = displayName;
            Color = color;
            Brush = new SolidColorBrush(color);
            Brush.Freeze();
        }

        // MaterialDesignColors enum name — round-tripped through the JSON
        // prefs file. Stable across language changes.
        public string Id { get; }

        // Russian label shown under the swatch in the picker.
        public string DisplayName { get; }

        public Color Color { get; }
        public SolidColorBrush Brush { get; }
    }
}
