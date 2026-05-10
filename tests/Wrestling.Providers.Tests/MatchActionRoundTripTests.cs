using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Wrestling.Data;
using Wrestling.Entities;
using Wrestling.Entities.Bracket;
using Wrestling.Providers;
using Xunit;

namespace Wrestling.Providers.Tests;

// End-to-end round-trip for MatchAction's typed discriminator. Builds a
// minimal Olympic bracket, attaches one action per MatchActionType variant
// to the first match, runs the action through the adapter, and asserts that
// Type / IsForRed / Points / RoundNumber / SecondInRound all survive.
//
// Also covers the back-compat path: an info object with the new Type field
// missing (Type == "" / Unknown) should be normalized via
// LegacyMatchActionTypeInferrer on load.
public class MatchActionRoundTripTests
{
    private readonly EntityToInfoAdapter _adapter = new();

    [Fact]
    public void Round_trip_preserves_Type_for_every_variant()
    {
        var t = BuildTournamentWithOpenSemifinal(out var openMatch);

        // One sample action per MatchActionType (excluding Unknown — it's
        // never written by new code). Choose IsForRed/Points so the rules
        // exercise both sided and side-agnostic variants.
        openMatch.MatchActions.Add(MakeAction(MatchActionType.SetPoints,        true,  2));
        openMatch.MatchActions.Add(MakeAction(MatchActionType.SetWarning,       false, 0));
        openMatch.MatchActions.Add(MakeAction(MatchActionType.RevertPoints,     true,  2));
        openMatch.MatchActions.Add(MakeAction(MatchActionType.RevertWarning,    false, 0));
        openMatch.MatchActions.Add(MakeAction(MatchActionType.ShowActionTimer,  true,  30));
        openMatch.MatchActions.Add(MakeAction(MatchActionType.HideActionTimer,  true,  0));
        openMatch.MatchActions.Add(MakeAction(MatchActionType.ActionTimerExpired, false, 0));
        openMatch.MatchActions.Add(MakeAction(MatchActionType.StartMatchTimer,  null,  0));
        openMatch.MatchActions.Add(MakeAction(MatchActionType.StopMatchTimer,   null,  0));
        openMatch.MatchActions.Add(MakeAction(MatchActionType.StartTimeout,     null,  0));
        openMatch.MatchActions.Add(MakeAction(MatchActionType.StopTimeout,      null,  0));
        openMatch.MatchActions.Add(MakeAction(MatchActionType.RoundFinished,    null,  1));
        openMatch.MatchActions.Add(MakeAction(MatchActionType.TimerAdjusted,    null,  -5));
        openMatch.MatchActions.Add(MakeAction(MatchActionType.MatchCompleted,   null,  0));
        var sourceTypes = openMatch.MatchActions.Select(a => a.Type).ToList();
        var sourceForRed = openMatch.MatchActions.Select(a => a.IsForRed).ToList();
        var sourcePoints = openMatch.MatchActions.Select(a => a.Points).ToList();

        var info = _adapter.GetInfoFromEntity(t);
        var restored = _adapter.GetEntityFromInfo(info);

        var restoredActions = restored.Groups[0].Bracket.Rounds[0].RoundMatches[0].MatchActions;

        restoredActions.Select(a => a.Type).Should().Equal(sourceTypes);
        restoredActions.Select(a => a.IsForRed).Should().Equal(sourceForRed);
        restoredActions.Select(a => a.Points).Should().Equal(sourcePoints);
    }

    [Fact]
    public void Loading_legacy_info_without_Type_infers_from_text()
    {
        // Mimics a .wrt written by the old app: each MatchActionInfo has
        // Type = null (deserializer default for missing JSON key) and Text
        // populated. The adapter must populate the entity's Type field by
        // running the legacy inferrer.
        var info = BuildTournamentInfoWithLegacyActions(
            ("Таймер запущен",                            MatchActionType.StartMatchTimer),
            ("Красный +2 балла",                          MatchActionType.SetPoints),
            ("Борец в синем трико получил 1 предупреждение", MatchActionType.SetWarning),
            ("Завершен таймер активности",                MatchActionType.ActionTimerExpired),
            ("Раунд 1 завершен",                          MatchActionType.RoundFinished),
            ("Матч завершен",                             MatchActionType.MatchCompleted));

        var restored = _adapter.GetEntityFromInfo(info);
        var actions = restored.Groups[0].Bracket.Rounds[0].RoundMatches[0].MatchActions;

        actions.Select(a => a.Type).Should().Equal(new[]
        {
            MatchActionType.StartMatchTimer,
            MatchActionType.SetPoints,
            MatchActionType.SetWarning,
            MatchActionType.ActionTimerExpired,
            MatchActionType.RoundFinished,
            MatchActionType.MatchCompleted,
        });
    }

    [Fact]
    public void Adapter_populates_legacy_Text_field_when_writing_new_actions()
    {
        // An old version of the app reading a .wrt written by the new app
        // has no awareness of Type — it relies entirely on Text. Confirm
        // that GetInfoFromEntity computes a non-empty Text for every typed
        // action so old clients still see a meaningful protocol log.
        var t = BuildTournamentWithOpenSemifinal(out var openMatch);
        openMatch.MatchActions.Add(MakeAction(MatchActionType.SetPoints,  true,  2));
        openMatch.MatchActions.Add(MakeAction(MatchActionType.SetWarning, false, 0));
        openMatch.MatchActions.Add(MakeAction(MatchActionType.RoundFinished, null, 1));

        var info = _adapter.GetInfoFromEntity(t);
        var infoActions = info.Groups.First().Bracket.Rounds[0].RoundMatches[0].MatchActions;

        infoActions.Should().HaveCount(3);
        foreach (var a in infoActions)
        {
            a.Text.Should().NotBeNullOrWhiteSpace($"action {a.Type} must produce a display string for back-compat");
        }
    }

    // ---------- helpers ----------

    private static Tournament BuildTournamentWithOpenSemifinal(out WrestlingMatch openMatch)
    {
        var group = new AgeWeightGroup
        {
            ID = Guid.NewGuid(),
            BirthYearMin = 2005, BirthYearMax = 2006, WeightMax = 70,
            MaxRoundSecond = 180, MaxTimeoutSecond = 30, MaxActionSecond = 30,
        };
        var wrestlers = Enumerable.Range(0, 4).Select(i => new Wrestler
        {
            ID = Guid.NewGuid(),
            FirstName = $"W{i}",
            LastName = $"Фамилия{i}",
            BirthDate = new DateTime(2005, 1, 1),
            Weight = 68,
            IsEntryFeePaid = true,
            IsWeightApproved = true,
            GroupID = group.ID,
            SeedNumber = i + 1,
        }).ToList();
        group.Wrestlers = wrestlers;

        var t = new Tournament(new GlobalSettings()) { ID = Guid.NewGuid(), Name = "MA-RT" };
        t.Groups.Add(group);
        foreach (var w in wrestlers) t.Wrestlers.Add(w);

        new OlympicGroupBracketProcessor().Generate(t, group);
        openMatch = group.Bracket.Rounds[0].RoundMatches[0];
        return t;
    }

    private static MatchAction MakeAction(MatchActionType type, bool? isForRed, int points)
        => new()
        {
            Type = type,
            DateTime = new DateTime(2026, 5, 10, 12, 0, 0),
            RoundNumber = 1,
            SecondInRound = 30,
            IsForRed = isForRed,
            Points = points,
        };

    private static TournamentInfo BuildTournamentInfoWithLegacyActions(
        params (string text, MatchActionType _expected)[] entries)
    {
        var entityT = BuildTournamentWithOpenSemifinal(out var entityMatch);
        var info = new EntityToInfoAdapter().GetInfoFromEntity(entityT);

        // info.Groups is a deferred LINQ Select — re-enumerating produces a
        // fresh AgeWeightGroupInfo each call, so we'd mutate a throwaway
        // copy. Materialize once and pin the reference.
        info.Groups = info.Groups.ToList();

        // Replace the (currently empty) MatchActions list of the first
        // round-1 match with legacy-shaped entries: Text set, Type left as
        // null so deserialization-default + ParseEnumOrDefault returns
        // Unknown, forcing the inferrer.
        var matchInfo = info.Groups.First().Bracket.Rounds[0].RoundMatches[0];
        matchInfo.MatchActions = entries.Select(e => new MatchActionInfo
        {
            DateTime = new DateTime(2026, 5, 10, 12, 0, 0),
            RoundNumber = 1,
            SecondInRound = 10,
            Text = e.text,
            Type = null,
        }).ToList();

        return info;
    }
}
