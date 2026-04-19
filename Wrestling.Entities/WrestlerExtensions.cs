using System;

namespace Wrestling.Entities
{
    public static class WrestlerExtensions
    {
        // Identity by ID with reference-equality fallback. Reference fallback
        // keeps the old behavior working for code paths that hold a single
        // Wrestler instance in flight (unit tests, freshly-constructed objects
        // without a set ID). Once the Wrestler has a real ID, Clone/Sync or
        // post-deserialization rehydration can't silently break bracket matching.
        public static bool SameAs(this Wrestler a, Wrestler b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a is null || b is null) return false;
            return a.ID != Guid.Empty && a.ID == b.ID;
        }
    }
}
