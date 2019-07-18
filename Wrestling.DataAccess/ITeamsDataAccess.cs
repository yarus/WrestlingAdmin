using System.Collections.Generic;
using Wrestling.Data;

namespace Wrestling.DataAccess
{
    public interface ITeamsDataAccess
    {
        List<TeamApplicationInfo> LoadFromFile(string fileName);
        bool SaveToFile(List<TeamApplicationInfo> list, string fileName);
    }
}