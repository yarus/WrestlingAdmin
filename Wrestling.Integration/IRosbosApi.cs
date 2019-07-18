using System.Collections.Generic;
using Wrestling.Entities;

namespace Wrestling.Integration
{
    public interface IRosbosApi
    {
        bool CheckConnection();
        bool LoadToken();
        void SetCredentials(string userName, string password);
        List<TeamApplication> GetTeams();
        List<Wrestler> GetWrestlers();
    }
}