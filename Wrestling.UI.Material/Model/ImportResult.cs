namespace Wrestling.UI.Material.Model
{
    public enum ImportOutcome
    {
        Imported,
        NoNewData,
        FileUnavailable,
        TournamentMismatch,
        Error
    }

    public readonly struct ImportResult
    {
        public ImportResult(ImportOutcome outcome, int importedCount)
        {
            Outcome = outcome;
            ImportedCount = importedCount;
        }

        public ImportOutcome Outcome { get; }
        public int ImportedCount { get; }

        public static ImportResult Imported(int count) => new ImportResult(ImportOutcome.Imported, count);
        public static ImportResult NoNewData() => new ImportResult(ImportOutcome.NoNewData, 0);
        public static ImportResult FileUnavailable() => new ImportResult(ImportOutcome.FileUnavailable, 0);
        public static ImportResult TournamentMismatch() => new ImportResult(ImportOutcome.TournamentMismatch, 0);
        public static ImportResult Error() => new ImportResult(ImportOutcome.Error, 0);
    }
}
