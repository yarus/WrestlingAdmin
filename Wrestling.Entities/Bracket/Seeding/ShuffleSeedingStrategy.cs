using System;
using System.Collections.Generic;
using System.Linq;

namespace Wrestling.Entities.Bracket.Seeding
{
    // Legacy strategy: random shuffle for everybody whose IsSeedFixed is false,
    // preserving slots of wrestlers already locked by a prior seeding. This is
    // what DrawViewModel.SeedWrestlers did before ClubCityLevelSeedingStrategy
    // was introduced; kept as a fallback for anyone who prefers a pure random
    // draw.
    public class ShuffleSeedingStrategy : ISeedingStrategy
    {
        public void Seed(AgeWeightGroup group)
        {
            var staticSeeds = new List<Wrestler>();
            var tmpSeeds = new List<Wrestler>();

            var rng = new Random();
            foreach (var wr in group.Wrestlers)
            {
                if (wr.IsSeedFixed && wr.SeedNumber.HasValue)
                {
                    staticSeeds.Add(wr);
                }
                else
                {
                    wr.IsSeedFixed = false;
                    wr.SeedNumber = rng.Next();
                    tmpSeeds.Add(wr);
                }
            }

            for (var i = 0; i < tmpSeeds.Count; i++)
            {
                int j = rng.Next(i, tmpSeeds.Count);
                (tmpSeeds[i], tmpSeeds[j]) = (tmpSeeds[j], tmpSeeds[i]);
            }

            foreach (var wr in staticSeeds.OrderBy(w => w.SeedNumber))
            {
                tmpSeeds.Add(wr);
            }

            tmpSeeds = tmpSeeds.OrderBy(w => w.SeedNumber).ToList();

            for (int i = 0; i < tmpSeeds.Count; i++)
            {
                tmpSeeds[i].SeedNumber = i + 1;
            }

            group.Wrestlers = tmpSeeds;
        }
    }
}
