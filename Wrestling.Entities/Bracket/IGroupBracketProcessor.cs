using System.Collections.Generic;
using Wrestling.Entities.Results;

namespace Wrestling.Entities.Bracket
{
    public interface IGroupBracketProcessor
    {
        string Title { get; }
        string Code { get; }
        int? AthletesMinCount { get; }
        int? AthletesMaxCount { get; }        
        void LoadTournamentGroup(Tournament tournament, AgeWeightGroup group);
        void Generate(Tournament tournament, AgeWeightGroup group);
        IEnumerable<TournamentResult> GetResults();
        void Load(Tournament tournament, AgeWeightGroup group);
        void CompleteMatch(WrestlingMatch wrestlingMatch, bool? isRedWon, MatchWinTypeEnum winType);
        void RevertMatch(WrestlingMatch wrestlingMatch);
        bool CanMatchBeReverted(WrestlingMatch wrestlingMatch);
        GroupRound GetSemiFinalRound(AgeWeightGroup group);
        List<GroupRound> GetMainQualificationRounds(AgeWeightGroup group);
        GroupRound Get3rdPlaceRound(AgeWeightGroup group);
        List<GroupRound> GetAdditionalQualificationRounds(AgeWeightGroup group);
        GroupRound GetFinalRound(AgeWeightGroup group);
    }
}