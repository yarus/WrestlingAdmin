using System;
using System.Collections.Generic;
using System.Linq;

namespace Wrestling.Entities.Bracket
{
    public sealed class GroupBracketProcessorFactory : IGroupBracketProcessorFactory
    {
        private readonly Dictionary<string, Func<IGroupBracketProcessor>> _factories;

        public GroupBracketProcessorFactory() : this(
            () => new OlympicWithConsolationFromFinalistsGroupBracketProcessor(),
            () => new OlympicGroupBracketProcessor(),
            () => new RoundRobinGroupBracketProcessor(),
            () => new SubGroupsToOlympicBracketProcessor())
        {
        }

        public GroupBracketProcessorFactory(params Func<IGroupBracketProcessor>[] factories)
        {
            _factories = factories.ToDictionary(create => create().Code, create => create);
        }

        public IGroupBracketProcessor Create(string code) =>
            code != null && _factories.TryGetValue(code, out var create) ? create() : null;

        public List<IGroupBracketProcessor> CreateAll() => _factories.Values.Select(create => create()).ToList();
    }
}
