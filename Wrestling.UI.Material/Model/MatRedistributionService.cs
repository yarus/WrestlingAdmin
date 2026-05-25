using System.Collections.Generic;
using System.Linq;
using Wrestling.Entities;
using Wrestling.Entities.Bracket;

namespace Wrestling.UI.Material.Model
{
    public sealed class MatRedistributionService : IMatRedistributionService
    {
        private readonly IMatchNumbersGenerator _matchNumbersGenerator;
        private readonly List<IGroupBracketProcessor> _processors;

        public MatRedistributionService(
            IMatchNumbersGenerator matchNumbersGenerator,
            List<IGroupBracketProcessor> processors)
        {
            _matchNumbersGenerator = matchNumbersGenerator;
            _processors = processors;
        }

        public bool HasLiveMatch(AgeWeightGroup group)
        {
            if (group?.Bracket?.Rounds == null) return false;
            foreach (var round in group.Bracket.Rounds)
            {
                foreach (var match in round.RoundMatches)
                {
                    if (match.Status == MatchStatusEnum.Pending && match.StartDateTime.HasValue)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public MoveResult MoveGroupToMat(
            Entities.Tournament tournament,
            AgeWeightGroup group,
            System.Nullable<System.Guid> targetMatId)
        {
            if (tournament == null || group == null)
            {
                return new MoveResult { Outcome = MoveOutcome.NoChange };
            }

            if (group.MatID == targetMatId)
            {
                return new MoveResult { Outcome = MoveOutcome.NoChange };
            }

            var live = FindLiveMatch(group);
            if (live != null)
            {
                return new MoveResult { Outcome = MoveOutcome.BlockedByLiveMatch, LiveMatch = live };
            }

            var oldMat = group.MatID.HasValue
                ? tournament.Mats.FirstOrDefault(m => m.ID == group.MatID.Value)
                : null;
            oldMat?.Groups.Remove(group);

            var newMat = targetMatId.HasValue
                ? tournament.Mats.FirstOrDefault(m => m.ID == targetMatId.Value)
                : null;

            group.MatID = newMat?.ID;
            group.MatLabel = newMat?.Name ?? string.Empty;
            group.FieldsVersion++;

            if (newMat != null && !newMat.Groups.Contains(group))
            {
                newMat.Groups.Add(group);
            }

            oldMat?.RefreshStats();
            newMat?.RefreshStats();

            _matchNumbersGenerator.Generate(tournament, _processors);

            return new MoveResult { Outcome = MoveOutcome.Moved };
        }

        public MoveResult MoveGroupToPart(
            Entities.Tournament tournament,
            AgeWeightGroup group,
            System.Guid targetPartId)
        {
            if (tournament == null || group == null)
            {
                return new MoveResult { Outcome = MoveOutcome.NoChange };
            }

            if (group.PartID == targetPartId)
            {
                return new MoveResult { Outcome = MoveOutcome.NoChange };
            }

            // Live match: same guard as MoveGroupToMat — the operator must
            // settle the in-progress match before reorganizing parts.
            var live = FindLiveMatch(group);
            if (live != null)
            {
                return new MoveResult { Outcome = MoveOutcome.BlockedByLiveMatch, LiveMatch = live };
            }

            // Completed matches in the source part are already part of that
            // part's PersonalResults / TeamResults computation. Moving the
            // group to another part would silently rewrite history. Block.
            var completedCount = CountCompleted(group);
            if (completedCount > 0)
            {
                return new MoveResult
                {
                    Outcome = MoveOutcome.BlockedByCompletedMatches,
                    CompletedMatchesCount = completedCount
                };
            }

            // Validate that the target part actually exists — defensive guard
            // against stale dropdowns where the part was deleted between the
            // popup opening and the click.
            var targetExists = false;
            foreach (var p in tournament.Parts)
            {
                if (p.ID == targetPartId) { targetExists = true; break; }
            }
            if (!targetExists)
            {
                return new MoveResult { Outcome = MoveOutcome.NoChange };
            }

            group.PartID = targetPartId;
            group.FieldsVersion++;

            // Per-(Part, Mat) numbering needs regenerating: the group's
            // matches migrate from one part's number space to another.
            _matchNumbersGenerator.Generate(tournament, _processors);

            return new MoveResult { Outcome = MoveOutcome.Moved };
        }

        // Count only "real" completed matches — auto-completed FreeWin byes
        // don't reflect a wrestled result, so they must not block a part move.
        private static int CountCompleted(AgeWeightGroup group)
        {
            if (group?.Bracket?.Rounds == null) return 0;
            int count = 0;
            foreach (var round in group.Bracket.Rounds)
            {
                foreach (var match in round.RoundMatches)
                {
                    if (match.Status == MatchStatusEnum.Completed
                        && match.WinType != MatchWinTypeEnum.FreeWin)
                    {
                        count++;
                    }
                }
            }
            return count;
        }

        private static WrestlingMatch FindLiveMatch(AgeWeightGroup group)
        {
            if (group?.Bracket?.Rounds == null) return null;
            foreach (var round in group.Bracket.Rounds)
            {
                foreach (var match in round.RoundMatches)
                {
                    if (match.Status == MatchStatusEnum.Pending && match.StartDateTime.HasValue)
                    {
                        return match;
                    }
                }
            }
            return null;
        }
    }
}
