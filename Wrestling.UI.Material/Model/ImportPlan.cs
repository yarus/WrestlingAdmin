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

        // Raw ImportSources entry (HTTP URL, UNC, or packed "http|unc") that
        // produced this plan. Stamped onto WrestlingMatch.ImportCompletionSource
        // when Case 1 applies a remote completion — the next tick compares this
        // string against the match's stored source before propagating a revert,
        // so a peer that hasn't seen the completion yet can't fight with the
        // peer that did.
        public string Source { get; set; }

        public bool NeedsApply => ShortCircuit == null && Remote != null;

        public static ImportPlan Skip(ImportOutcome outcome) => new ImportPlan { ShortCircuit = outcome };
        public static ImportPlan Proceed(Entities.Tournament remote, string source) => new ImportPlan { Remote = remote, Source = source };
    }
}
