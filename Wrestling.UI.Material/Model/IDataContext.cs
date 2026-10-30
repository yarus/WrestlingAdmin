using System;
using System.Collections.Generic;
using Wrestling.Entities;

namespace Wrestling.UI.Material.Model
{
    public interface IDataContext
    {
        AgeWeightGroup Group { get; set; }
        WrestlingMatch WrestlingMatch { get; set; }
        Entities.Tournament Tournament { get; set; }
        TeamApplicationViewModel Team { get; set; }
        // IsBracketView (legacy match-return hint) was removed in the shell
        // refactor; overlay return now goes through IShellViewModel.GetReturnVmType().

        List<Wrestler> WrestlersCache { get; set; }
        List<TeamApplication> TeamsCache { get; set; }

        // Fires after the Tournament property changes (set to new instance,
        // swapped, or cleared to null). Sent with the new value so subscribers
        // can react without re-querying the property. Used by the network
        // services to bring the UDP announcer and HTTP server in and out of
        // lockstep with the open tournament.
        event EventHandler<Entities.Tournament> TournamentChanged;
    }
}