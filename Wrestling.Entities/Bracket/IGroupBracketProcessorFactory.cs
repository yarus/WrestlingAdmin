using System.Collections.Generic;

namespace Wrestling.Entities.Bracket
{
    public interface IGroupBracketProcessorFactory
    {
        IGroupBracketProcessor Create(string code);
        List<IGroupBracketProcessor> CreateAll();
    }
}
