namespace Wrestling.UI.Material.Model
{
    // Output of the threadpool-safe "prepare" phase of an import. Carries the
    // remote tournament (fully loaded and adapted off-UI) or a short-circuit
    // outcome for cases where applying is unnecessary (file missing, mismatch).
    // Consumed by the UI-thread "apply" phase to merge changes into the live
    // tournament's ObservableCollections.
    public sealed class ImportPlan
    {
        public ImportOutcome? ShortCircuit { get; set; }
        public Entities.Tournament Remote { get; set; }

        public bool NeedsApply => ShortCircuit == null && Remote != null;

        public static ImportPlan Skip(ImportOutcome outcome) => new ImportPlan { ShortCircuit = outcome };
        public static ImportPlan Proceed(Entities.Tournament remote) => new ImportPlan { Remote = remote };
    }
}
