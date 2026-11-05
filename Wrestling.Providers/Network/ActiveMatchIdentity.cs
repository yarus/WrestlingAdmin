using System;
using Newtonsoft.Json;

namespace Wrestling.Providers.Network
{
    // Runtime presence only. Never persisted in a tournament or included in its state hash.
    public sealed record ActiveMatchIdentity(
        [property: JsonProperty("groupId")] Guid GroupId,
        [property: JsonProperty("bracketVersion")] int BracketVersion,
        [property: JsonProperty("bracketFullNumber")] string BracketFullNumber);
}
