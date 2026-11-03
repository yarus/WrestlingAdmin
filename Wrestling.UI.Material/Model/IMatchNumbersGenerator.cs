using System.Collections.Generic;
using Wrestling.Entities.Bracket;

namespace Wrestling.UI.Material.Model
{
    public interface IMatchNumbersGenerator
    {
        void Generate(Entities.Tournament tournament, IGroupBracketProcessorFactory processors);
    }
}