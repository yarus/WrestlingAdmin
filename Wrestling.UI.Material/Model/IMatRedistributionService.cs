using Wrestling.Entities;

namespace Wrestling.UI.Material.Model
{
    public enum MoveOutcome
    {
        Moved,
        NoChange,
        BlockedByLiveMatch,
        // Moving a group between parts after any match has been Completed
        // would retroactively edit the previous part's standings (PartA's
        // PersonalResults already includes those completions). Blocked by
        // design — only MoveGroupToPart raises this outcome.
        BlockedByCompletedMatches
    }

    public sealed class MoveResult
    {
        public MoveOutcome Outcome { get; set; }
        public WrestlingMatch LiveMatch { get; set; }
        public int CompletedMatchesCount { get; set; }
    }

    // Centralises the group↔mat mutation path so the existing "Расписание"
    // screen and the new "Доска ковров" share one implementation. Each move
    // bumps FieldsVersion and re-runs MatMatchNumbersGenerator so the
    // numbering on both donor and recipient mats converges deterministically.
    // Peers pick up the change via the existing FieldsVersion-based import.
    public interface IMatRedistributionService
    {
        // Returns true when the group has a Pending match with StartDateTime
        // set — i.e., scoring has started on a mat but Approve / Revert hasn't
        // run yet. Moving such a group renumbers its matches under the live
        // scorer's feet, so the UI uses this to block the action.
        bool HasLiveMatch(AgeWeightGroup group);

        // Reassigns group to the target mat (null = unbind). Returns the outcome:
        //   Moved  — state changed; FieldsVersion was bumped and numbers regenerated.
        //   NoChange — group is already on the target mat; nothing was touched.
        //   BlockedByLiveMatch — group has an in-progress match; nothing was touched.
        //                        LiveMatch is populated so the UI can name the conflict.
        MoveResult MoveGroupToMat(
            Entities.Tournament tournament,
            AgeWeightGroup group,
            System.Nullable<System.Guid> targetMatId);

        // Reassigns group to the target part. MatID is preserved — the same
        // physical mat is fine, just under a different part. Returns:
        //   Moved — PartID changed, FieldsVersion + MetaVersion bumped,
        //           numbering regenerated.
        //   NoChange — already on the target part.
        //   BlockedByLiveMatch — group has a live match.
        //   BlockedByCompletedMatches — group has any completed matches; moving
        //                               would break the previous part's results.
        MoveResult MoveGroupToPart(
            Entities.Tournament tournament,
            AgeWeightGroup group,
            System.Guid targetPartId);
    }
}
