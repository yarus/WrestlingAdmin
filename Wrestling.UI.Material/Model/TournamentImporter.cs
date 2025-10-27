using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Wrestling.Entities;
using Wrestling.Entities.Bracket;
using Wrestling.Providers;

namespace Wrestling.UI.Material.Model
{
    public class TournamentImporter : ITournamentImporter
    {
        private readonly List<IGroupBracketProcessor> _drawTypes;

        private readonly ITournamentsManager _tournService;

        public TournamentImporter(ITournamentsManager tournService, List<IGroupBracketProcessor> processors)
        {
            _tournService = tournService;
            _drawTypes = processors;
        }

        public async Task<int> ImportDataFromFileAsync(Entities.Tournament target, string fileName)
        {
            int result = 0;

            if (string.IsNullOrEmpty(fileName)) return -1;

            var tournament = await _tournService.LoadFromFileAsync(fileName);

            if (tournament == null || tournament.Name != target.Name ||
                tournament.Groups.Count != target.Groups.Count || tournament.StartDate != target.StartDate) return -1;

            foreach (var group in tournament.Groups)
            {
                var sameGroup = target.Groups.FirstOrDefault(p => p.ID == group.ID
                                                                  && p.Bracket != null &&
                                                                  group.Bracket != null &&
                                                                  p.CarpetID == group.CarpetID &&
                                                                  p.Bracket.BracketTypeCode == group.Bracket.BracketTypeCode);
                if (sameGroup == null) continue;

                var matches = sameGroup.Bracket.Rounds.SelectMany(p => p.RoundMatches).ToList();
                var importedMatches = group.Bracket.Rounds.SelectMany(p => p.RoundMatches).ToList();

                if (matches.Count != importedMatches.Count) continue;

                foreach (var importedMatch in importedMatches)
                {
                    var baseMatch = matches.FirstOrDefault(p => p.MatchNumber == importedMatch.MatchNumber);
                    if (baseMatch != null && baseMatch.Status == MatchStatusEnum.Pending && importedMatch.Status == MatchStatusEnum.Completed)
                    {
                        baseMatch.WinType = importedMatch.WinType;
                        baseMatch.LastSecondInMatch = importedMatch.LastSecondInMatch;
                        baseMatch.PointsBlue = importedMatch.PointsBlue;
                        baseMatch.PointsRed = importedMatch.PointsRed;
                        baseMatch.WarningsNumberBlue = importedMatch.WarningsNumberBlue;
                        baseMatch.WarningsNumberRed = importedMatch.WarningsNumberRed;
                        baseMatch.IsRedWon = importedMatch.IsRedWon;
                        baseMatch.Note = importedMatch.Note;
                        baseMatch.MatchActions = new List<MatchAction>(importedMatch.MatchActions);

                        var processor = GetProcessoryForGroup(sameGroup.Bracket.BracketTypeCode);
                        if (processor == null) throw new ApplicationException("Can't find processor!");

                        processor.Load(target, sameGroup);
                        processor.CompleteMatch(baseMatch, baseMatch.IsRedWon.Value, baseMatch.WinType.Value);

                        result++;
                    }
                }
            }
            
            // Sync wrestler info (supporting changing of names)
            foreach (var wrestler in tournament.Wrestlers)
            {
                var changedWrestler = target.Wrestlers.FirstOrDefault(x => x.ID == wrestler.ID);
                
                if (changedWrestler == null || changedWrestler.Timestamp >= wrestler.Timestamp) continue;
                
                changedWrestler.Sync(wrestler);
            }

            return result;
        }

        private IGroupBracketProcessor GetProcessoryForGroup(string processorType)
        {
            return _drawTypes.FirstOrDefault(p => p.Code == processorType);
        }
    }
}
