namespace Wrestling.UI.Material.Theme
{
    public interface ILocalUiSettingsStorage
    {
        // Returns defaults on missing/corrupt — never throws. Mirrors the
        // tolerance policy of the .wrt loader (see CLAUDE.md "Load paths
        // never throw"); theme prefs failing to load must not block app
        // startup.
        LocalUiSettings Load();
        void Save(LocalUiSettings settings);
    }
}
