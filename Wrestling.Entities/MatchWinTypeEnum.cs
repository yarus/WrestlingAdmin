namespace Wrestling.Entities
{
    public enum MatchWinTypeEnum
    {
        Tushe,
        Injury,
        WarningsLimit,
        NoShow,
        DisqualifyWin,
        DominationWin,
        DominationWinWithPoints,
        PointsWin,
        PointsWinWithPoints,
        ActionWin,
        FreeWin,
        MutualDisqualify,
        // Both wrestlers withdraw due to injury (UWW «обоюдная травма»). The
        // match is completed with no winner (IsRedWon=null). Downstream
        // propagation mirrors mutual DSQ — opponent in the next match auto-
        // FreeWins. Wrestlers retain their place by elimination round; no DSQ
        // / NoShow flag is set since the withdrawal isn't a sanction.
        MutualInjury,
        // Both wrestlers fail to appear (UWW «обоюдная неявка»). Treated like
        // mutual DSQ — both placeless, 0 CP — but UI shows «Неявка» badge
        // (IsNoShow flag) instead of «DSQ».
        MutualNoShow
    }
}