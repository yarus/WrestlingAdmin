namespace Wrestling.Entities
{
    // Wrestler sport rank. Persisted into .wrt as the enum *name* (e.g.
    // "MSMK") via Newtonsoft's StringEnumConverter on WrestlerInfo.Level.
    // Display labels live in EntityLocalization (Level_* keys) so locale
    // changes flip the picker without touching the data.
    //
    // None = absent rank ("б/р" / "no rank") — the default for new wrestlers
    // and the sentinel for "unknown" when an old .wrt has a value the
    // adapter doesn't recognize. Numeric values are explicit so the JSON
    // wire format stays stable across enum-order changes.
    public enum WrestlerLevelEnum
    {
        None = 0,
        MSMK = 1,    // Мастер спорта международного класса
        MS = 2,      // Мастер спорта
        KMS = 3,     // Кандидат в мастера спорта
        Adult1 = 4,  // I взрослый
        Adult2 = 5,  // II взрослый
        Adult3 = 6,  // III взрослый
        Junior1 = 7, // I юношеский
        Junior2 = 8, // II юношеский
        Junior3 = 9, // III юношеский
    }
}
