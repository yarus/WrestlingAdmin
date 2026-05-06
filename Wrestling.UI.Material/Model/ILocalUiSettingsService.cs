using System;

namespace Wrestling.UI.Material.Model
{
    // Per-PC UI preferences — kept OUT of the .wrt because each carpet laptop
    // has its own "active carpet" selection that must not be overwritten by
    // peer-sync. Backed by a JSON file under %LocalAppData%/WrestlingAdmin/.
    // Read/Write are best-effort: a corrupt or missing file falls back to
    // defaults rather than throwing — same load-tolerance principle as .wrt.
    public interface ILocalUiSettingsService
    {
        Guid? LoadCarpetId();

        void SaveCarpetId(Guid? carpetId);

        bool LoadIsBracketsView();

        void SaveIsBracketsView(bool isBracketsView);
    }
}
