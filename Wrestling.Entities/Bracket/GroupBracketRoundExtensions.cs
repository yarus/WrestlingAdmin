using System.Collections.Generic;
using System.Linq;

namespace Wrestling.Entities.Bracket
{
    public static class GroupBracketRoundExtensions
    {
        public static IEnumerable<GroupRound> MainRounds(this GroupBracket bracket)
            => bracket?.Rounds?.Where(r => r.RoundType == GroupRoundTypeEnum.Main) ?? Enumerable.Empty<GroupRound>();

        public static IEnumerable<GroupRound> AdditionalRounds(this GroupBracket bracket)
            => bracket?.Rounds?.Where(r => r.RoundType == GroupRoundTypeEnum.Additional) ?? Enumerable.Empty<GroupRound>();

        public static IEnumerable<WrestlingMatch> AllMatches(this GroupBracket bracket)
            => bracket?.Rounds?.SelectMany(r => r.RoundMatches) ?? Enumerable.Empty<WrestlingMatch>();
    }
}
