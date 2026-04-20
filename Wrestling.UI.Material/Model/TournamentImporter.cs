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

        public async Task<ImportPlan> PrepareAsync(Entities.Tournament target, string fileName)
        {
            // Phase 1 — threadpool-safe. Loads + deserializes + runs the entity
            // adapter (the expensive 50-200ms CPU step) without touching the
            // target's live ObservableCollections. The caller is expected to
            // wrap this in Task.Run so the work happens off the UI thread.

            if (string.IsNullOrEmpty(fileName)) return ImportPlan.Skip(ImportOutcome.FileUnavailable);

            var tournament = await _tournService.LoadFromFileAsync(fileName).ConfigureAwait(false);

            if (tournament == null) return ImportPlan.Skip(ImportOutcome.FileUnavailable);

            if (tournament.Name != target.Name ||
                tournament.Groups.Count != target.Groups.Count ||
                tournament.StartDate != target.StartDate)
            {
                return ImportPlan.Skip(ImportOutcome.TournamentMismatch);
            }

            return ImportPlan.Proceed(tournament);
        }

        public ImportResult Apply(Entities.Tournament target, ImportPlan plan)
        {
            // Phase 2 — UI-thread. Walks the remote tournament loaded by
            // PrepareAsync and merges genuinely-new completions into the live
            // target via the bracket processors. Only the matches that actually
            // flipped from Pending to Completed touch ObservableCollection, so
            // the typical per-tick cost is < 10 ms even under a live round.

            if (plan == null) return new ImportResult(ImportOutcome.Error, 0);
            if (plan.ShortCircuit.HasValue) return new ImportResult(plan.ShortCircuit.Value, 0);
            if (plan.Remote == null) return new ImportResult(ImportOutcome.Error, 0);

            int result = 0;
            var tournament = plan.Remote;

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

                        var processor = GetProcessorForGroup(sameGroup.Bracket.BracketTypeCode);
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

            return result > 0 ? ImportResult.Imported(result) : ImportResult.NoNewData();
        }

        private IGroupBracketProcessor GetProcessorForGroup(string processorType)
        {
            return _drawTypes.FirstOrDefault(p => p.Code == processorType);
        }
    }
}
