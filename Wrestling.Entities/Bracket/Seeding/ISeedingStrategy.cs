namespace Wrestling.Entities.Bracket.Seeding
{
    // Encapsulates the "how do we assign SeedNumber within a group" decision.
    // Implementations rewrite group.Wrestlers order and each Wrestler's
    // SeedNumber / IsSeedFixed. The invariant after Seed(): Wrestlers[i].SeedNumber == i+1
    // for every wrestler in the group.
    //
    // Wrestlers entering with IsSeedFixed=true and a valid SeedNumber act as
    // locks — the strategy may not move them. All other wrestlers are free
    // to place.
    public interface ISeedingStrategy
    {
        void Seed(AgeWeightGroup group);
    }
}
