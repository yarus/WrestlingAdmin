using System.Collections.Generic;
using MaterialDesignThemes.Wpf;
using Wrestling.UI.Material.Model;

namespace Wrestling.UI.Material.Tournament.Phase5
{
    // Marker for the three sub-tabs that share the Phase 5 ("Проведение")
    // top-tab strip: Ковер / Слайдер / Администратор. The wrapper
    // (Phase5ViewModel) forwards InitData and surfaces QuickButtons.
    public interface IPhase5SubViewModel
    {
        void InitData();

        string PageName { get; }

        PackIconKind IconKind { get; }

        IList<CommandButtonItem> QuickButtons { get; }
    }
}
