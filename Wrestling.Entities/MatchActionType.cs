namespace Wrestling.Entities
{
    // Discriminator for MatchAction. Carried in the .wrt schema as a string
    // (see EntityToInfoAdapter), so reordering existing variants is safe but
    // renaming is not — old files referencing the old name would map to Unknown.
    //
    // Unknown is the deserialization default for legacy actions written before
    // typing was introduced. The adapter normalizes Unknown by inferring the
    // real type from MatchActionInfo.Text via LegacyMatchActionTypeInferrer.
    public enum MatchActionType
    {
        Unknown = 0,
        SetPoints,
        SetWarning,
        RevertPoints,
        RevertWarning,
        ShowActionTimer,
        HideActionTimer,
        ActionTimerExpired,
        StartMatchTimer,
        StopMatchTimer,
        StartTimeout,
        StopTimeout,
        RoundFinished,
        TimerAdjusted,
        MatchCompleted,
    }
}
