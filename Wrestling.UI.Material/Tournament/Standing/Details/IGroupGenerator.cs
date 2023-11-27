using System.Collections.Generic;
using FluentResults;
using Wrestling.Entities;

namespace Wrestling.UI.Material.Tournament.Standing.Details
{
    public interface IGroupGenerator
    {
        Result<IReadOnlyList<AgeWeightGroup>> Generate(string statement, GlobalSettings settings = null);
    }   
}