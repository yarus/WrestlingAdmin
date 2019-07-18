using System.Collections.Generic;
using Wrestling.UI.Material.Model;

namespace Wrestling.UI.Material.Tournament.Standing
{
    public interface IStandingPageViewModel
    {
        void InitData();
        string PageName { get; }
        string PageTitle { get; }
        IList<CommandButtonItem> QuickButtons { get; }
    }
}