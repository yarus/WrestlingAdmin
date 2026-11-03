using System;
using System.Collections.Generic;

namespace Wrestling.UI.Material.Model
{
    public enum ImportOutcome
    {
        Imported,
        NoNewData,
        FileUnavailable,
        TournamentMismatch,
        Error,
        Conflict
    }

    public readonly struct ImportResult
    {
        public ImportResult(ImportOutcome outcome, int importedCount, IReadOnlyList<ImportConflict> conflicts = null)
        {
            Outcome = outcome;
            ImportedCount = importedCount;
            Conflicts = conflicts ?? Array.Empty<ImportConflict>();
        }

        public ImportOutcome Outcome { get; }
        public int ImportedCount { get; }
        public IReadOnlyList<ImportConflict> Conflicts { get; }

        public static ImportResult Imported(int count) => new ImportResult(ImportOutcome.Imported, count);
        public static ImportResult NoNewData() => new ImportResult(ImportOutcome.NoNewData, 0);
        public static ImportResult FileUnavailable() => new ImportResult(ImportOutcome.FileUnavailable, 0);
        public static ImportResult TournamentMismatch() => new ImportResult(ImportOutcome.TournamentMismatch, 0);
        public static ImportResult Error() => new ImportResult(ImportOutcome.Error, 0);
    }

    public enum ImportConflictReason { DifferentResult, DifferentParticipants, CompletedDependentMatch, DifferentStructure, InvalidResult }

    public sealed record ImportConflict(Guid GroupId, string GroupName, int? MatchNumber, string MatchIdentity, ImportConflictReason Reason);
}
