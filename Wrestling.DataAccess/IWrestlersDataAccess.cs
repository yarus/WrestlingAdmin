using System.Collections.Generic;
using Wrestling.Data;

namespace Wrestling.DataAccess
{
    public interface IWrestlersDataAccess
    {
        List<WrestlerInfo> LoadFromFile(string fileName);
        bool SaveToFile(List<WrestlerInfo> list, string fileName);
    }
}