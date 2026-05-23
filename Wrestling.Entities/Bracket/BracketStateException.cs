using System;

namespace Wrestling.Entities.Bracket
{
    // Thrown when the bracket graph is in an inconsistent state — a forward
    // link refers to a non-existent match, a sibling lookup fails, or a
    // cascade can't find a wrestler that should be present. Always indicates
    // a code bug or a corrupted .wrt file: the global crash handler logs it
    // and writes a backup so the operator can recover.
    //
    // Distinguished from InvalidOperationException at the catch site: a
    // BracketStateException is recoverable via «open backup .wrt», while a
    // generic InvalidOperationException usually means a precondition was
    // violated by caller code (a UI button enabled when it shouldn't be).
    public sealed class BracketStateException : InvalidOperationException
    {
        public BracketStateException(string message) : base(message) { }
        public BracketStateException(string message, Exception innerException) : base(message, innerException) { }
    }
}
