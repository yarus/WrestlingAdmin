using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Newtonsoft.Json;
using Wrestling.Data;
using Wrestling.Entities;
using Wrestling.Entities.Bracket;
using Wrestling.Providers;
using Wrestling.UI.Material.Model;
using Xunit;
using WTournament = Wrestling.Entities.Tournament;

namespace Wrestling.UI.Material.Tests;

public class SubGroupsBranchSyncTests
{
    private static WTournament Clone(WTournament t)
    {
        var adapter = new EntityToInfoAdapter();
        return adapter.GetEntityFromInfo(JsonConvert.DeserializeObject<TournamentInfo>(JsonConvert.SerializeObject(adapter.GetInfoFromEntity(t))));
    }

    private static (WTournament T, AgeWeightGroup G, SubGroupsToOlympicBracketProcessor P) Setup(int count = 6)
    {
        var t = new WTournament(new GlobalSettings()) { ID = Guid.NewGuid() };
        var part = new TournamentPart { ID = Guid.NewGuid(), Name = "Part" }; t.Parts.Add(part);
        var g = new AgeWeightGroup { ID = Guid.NewGuid(), PartID = part.ID, MaxRoundSecond = 120, MaxTimeoutSecond = 30, MaxActionSecond = 30 };
        t.Groups.Add(g);
        for (int i = 0; i < count; i++)
        {
            var w = new Wrestler { ID = Guid.NewGuid(), GroupID = g.ID, FirstName = "Test", LastName = "W" + i, BirthDate = new DateTime(2013, 1, 1), Weight = 40, IsEntryFeePaid = true, IsWeightApproved = true, SeedNumber = i + 1 };
            t.Wrestlers.Add(w); g.Wrestlers.Add(w);
        }
        var p = new SubGroupsToOlympicBracketProcessor(); p.Generate(t, g);
        return (t, g, p);
    }

    private static List<WrestlingMatch> Matches(AgeWeightGroup g) => g.Bracket.Rounds.SelectMany(r => r.RoundMatches).ToList();
    private static string State(AgeWeightGroup g) => string.Join(";", Matches(g).Select(m => $"{m.BracketFullNumber}:{m.Status}:{m.WinType}:{m.IsRedWon}:{m.WrestlerInRed?.ID}:{m.WrestlerInBlue?.ID}"));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Shared_injury_medal_places_survive_reload_sync_and_result_edit(bool bronze)
    {
        var (t, g, p) = Setup();
        var peer = Clone(t);
        var target = bronze ? p.Get3rdPlaceRound(g).RoundMatches.Single() : p.GetFinalRound(g).RoundMatches.Single();
        for (var step = 0; step < 30; step++)
        {
            var available = Matches(g).Where(m => m.IsMatchCanStart).ToList();
            if (available.Count == 0) break;
            var match = available.Contains(target) ? target : available.First();
            match.Version++;
            p.CompleteMatch(match, match == target ? null : true,
                match == target ? MatchWinTypeEnum.MutualInjury : MatchWinTypeEnum.PointsWin);
        }
        g.IsBracketCompleted.Should().BeTrue();
        var importer = new TournamentImporter(null, new List<IGroupBracketProcessor> { new SubGroupsToOlympicBracketProcessor() }, null);
        importer.Apply(peer, ImportPlan.Proceed(Clone(t)));
        foreach (var restored in new[] { Clone(t), peer })
        {
            var restoredGroup = restored.Groups.Single();
            var restoredProcessor = new SubGroupsToOlympicBracketProcessor();
            restoredProcessor.Load(restored, restoredGroup);
            restoredProcessor.GetResults().ToList();
            var medal = Matches(restoredGroup).Single(m => m.BracketFullNumber == target.BracketFullNumber);
            medal.WrestlerInRed.FinalPlace.Should().Be(bronze ? 3 : 1);
            medal.WrestlerInBlue.FinalPlace.Should().Be(bronze ? 3 : 1);
        }

        p.RevertMatch(target);
        target.Version++;
        p.CompleteMatch(target, false, MatchWinTypeEnum.PointsWin);
        importer.Apply(peer, ImportPlan.Proceed(Clone(t)));
        var peerProcessor = new SubGroupsToOlympicBracketProcessor();
        peerProcessor.Load(peer, peer.Groups.Single());
        peerProcessor.GetResults().ToList();
        var edited = Matches(peer.Groups.Single()).Single(m => m.BracketFullNumber == target.BracketFullNumber);
        edited.WrestlerInBlue.FinalPlace.Should().Be(bronze ? 3 : 1);
        edited.WrestlerInRed.FinalPlace.Should().Be(bronze ? 4 : 2);
    }

    [Theory]
    [InlineData(MatchWinTypeEnum.MutualDisqualify)]
    [InlineData(MatchWinTypeEnum.MutualNoShow)]
    [InlineData(MatchWinTypeEnum.MutualInjury)]
    public void Automatic_bronze_completion_and_semifinal_revert_round_trip_and_sync(MatchWinTypeEnum type)
    {
        var (t, g, p) = Setup();
        var peer = Clone(t);
        var importer = new TournamentImporter(null, new List<IGroupBracketProcessor> { new SubGroupsToOlympicBracketProcessor() }, null);
        var mutual = g.Bracket.MainRounds().First().RoundMatches[0];
        mutual.Version++;
        p.CompleteMatch(mutual, null, type);
        while (g.Bracket.MainRounds().SelectMany(r => r.RoundMatches).Any(m => m.IsMatchCanStart))
        {
            var m = g.Bracket.MainRounds().SelectMany(r => r.RoundMatches).First(m => m.IsMatchCanStart);
            m.Version++; p.CompleteMatch(m, true, MatchWinTypeEnum.PointsWin);
        }
        var sf = p.GetSemiFinalRound(g).RoundMatches.Single(m => m.IsMatchCanStart);
        sf.Version++; p.CompleteMatch(sf, true, MatchWinTypeEnum.PointsWin);
        importer.Apply(peer, ImportPlan.Proceed(Clone(t)));
        State(peer.Groups.Single()).Should().Be(State(g));
        var reopened = Clone(t);
        p.Load(reopened, reopened.Groups.Single());
        State(reopened.Groups.Single()).Should().Be(State(g));
        p.Load(t, g);

        p.RevertMatch(sf); sf.Version++;
        importer.Apply(peer, ImportPlan.Proceed(Clone(t)));
        State(peer.Groups.Single()).Should().Be(State(g));
        var peerBronze = peer.Groups.Single().Bracket.AdditionalRounds().Last().RoundMatches.Single();
        peerBronze.Status.Should().Be(MatchStatusEnum.Pending);

        sf.Version++; p.CompleteMatch(sf, false, MatchWinTypeEnum.PointsWin);
        importer.Apply(peer, ImportPlan.Proceed(Clone(t)));
        State(peer.Groups.Single()).Should().Be(State(g));
        peerBronze.Status.Should().Be(MatchStatusEnum.Completed);
        while (Matches(g).Any(m => m.IsMatchCanStart))
        {
            var m = Matches(g).First(m => m.IsMatchCanStart); m.Version++; p.CompleteMatch(m, true, MatchWinTypeEnum.PointsWin);
        }
        importer.Apply(peer, ImportPlan.Proceed(Clone(t)));
        var peerP = new SubGroupsToOlympicBracketProcessor(); peerP.Load(peer, peer.Groups.Single());
        p.GetResults(); peerP.GetResults();
        peer.Groups.Single().IsBracketCompleted.Should().BeTrue();
        peer.Wrestlers.OrderBy(w => w.ID).Select(w => w.FinalPlace).Should().Equal(t.Wrestlers.OrderBy(w => w.ID).Select(w => w.FinalPlace));
    }

    [Theory]
    [InlineData(MatchWinTypeEnum.MutualDisqualify)]
    [InlineData(MatchWinTypeEnum.MutualNoShow)]
    public void Replacement_history_flags_and_undo_round_trip_and_merge_atomically(MatchWinTypeEnum type)
    {
        var (t, g, p) = Setup();
        foreach (var m in g.Bracket.MainRounds().SelectMany(r => r.RoundMatches))
        {
            m.Version++;
            p.CompleteMatch(m, true, MatchWinTypeEnum.PointsWin);
        }
        var peer = Clone(t);
        var sf = p.GetSemiFinalRound(g).RoundMatches[0];
        var originalRed = sf.WrestlerInRed;
        var originalBlue = sf.WrestlerInBlue;
        sf.PointsRed = 4;
        sf.Note = "original mutual outcome";
        sf.MatchActions.Add(new MatchAction { Type = MatchActionType.SetPoints, Points = 4, IsForRed = true });
        sf.Version++;
        p.CompleteMatch(sf, null, type);
        var importer = new TournamentImporter(null, new List<IGroupBracketProcessor> { new SubGroupsToOlympicBracketProcessor() }, null);

        importer.Apply(peer, ImportPlan.Proceed(Clone(t)));

        var peerGroup = peer.Groups.Single();
        var peerSf = peerGroup.Bracket.Rounds.SelectMany(r => r.RoundMatches).Single(m => m.BracketFullNumber == sf.BracketFullNumber);
        peerGroup.BracketVersion.Should().Be(g.BracketVersion);
        State(peerGroup).Should().Be(State(g));
        var prior = peerSf.PreviousResults.Should().ContainSingle().Which;
        prior.WinType.Should().Be(type);
        prior.PointsRed.Should().Be(4);
        prior.Note.Should().Be("original mutual outcome");
        prior.MatchActions.Should().ContainSingle().Which.Points.Should().Be(4);
        prior.WrestlerInRed.Should().BeSameAs(peer.Wrestlers.Single(w => w.ID == originalRed.ID));
        prior.WrestlerInBlue.Should().BeSameAs(peer.Wrestlers.Single(w => w.ID == originalBlue.ID));
        prior.WrestlerInRed.IsPlaceless.Should().BeTrue();
        prior.WrestlerInBlue.IsPlaceless.Should().BeTrue();

        p.RevertMatch(sf);
        importer.Apply(peer, ImportPlan.Proceed(Clone(t)));

        peerSf = peerGroup.Bracket.Rounds.SelectMany(r => r.RoundMatches).Single(m => m.BracketFullNumber == sf.BracketFullNumber);
        State(peerGroup).Should().Be(State(g));
        peerSf.PreviousResults.Should().BeEmpty();
        peer.Wrestlers.Single(w => w.ID == originalRed.ID).IsPlaceless.Should().BeFalse();
        peer.Wrestlers.Single(w => w.ID == originalBlue.ID).IsPlaceless.Should().BeFalse();
        peerGroup.BracketVersion.Should().Be(g.BracketVersion);

        sf.Version++;
        p.CompleteMatch(sf, null, type);
        importer.Apply(peer, ImportPlan.Proceed(Clone(t)));
        sf.Version++;
        p.CompleteMatch(sf, false, MatchWinTypeEnum.PointsWin);
        importer.Apply(peer, ImportPlan.Proceed(Clone(t)));
        State(peerGroup).Should().Be(State(g));
        peerGroup.Bracket.Rounds.SelectMany(r => r.RoundMatches).Single(m => m.BracketFullNumber == sf.BracketFullNumber)
            .PreviousResults.Should().ContainSingle();
    }

    [Fact]
    public void Loading_a_saved_replacement_keeps_its_pool_selection_and_does_not_replace_it_again()
    {
        var (t, g, p) = Setup(7);
        foreach (var m in g.Bracket.MainRounds().SelectMany(r => r.RoundMatches))
            if (m.IsMatchCanStart) p.CompleteMatch(m, true, MatchWinTypeEnum.PointsWin);
        var sf = p.GetSemiFinalRound(g).RoundMatches[0];
        p.CompleteMatch(sf, null, MatchWinTypeEnum.MutualDisqualify);
        var version = g.BracketVersion;
        var restored = Clone(t);
        var restoredGroup = restored.Groups.Single();
        p.Load(restored, restoredGroup);
        p.Load(restored, restoredGroup);

        restoredGroup.BracketVersion.Should().Be(version);
        State(restoredGroup).Should().Be(State(g));
        restoredGroup.Bracket.Rounds.SelectMany(r => r.RoundMatches).Single(m => m.BracketFullNumber == sf.BracketFullNumber)
            .PreviousResults.Should().ContainSingle();
    }

    [Fact]
    public void Repeated_replacements_and_clearing_the_first_disqualification_merge_with_their_history()
    {
        var (t, g, p) = Setup(7);
        foreach (var m in g.Bracket.MainRounds().SelectMany(r => r.RoundMatches))
            if (m.IsMatchCanStart) { m.Version++; p.CompleteMatch(m, true, MatchWinTypeEnum.PointsWin); }
        var peer = Clone(t);
        var sf = p.GetSemiFinalRound(g).RoundMatches[0];
        var original = sf.WrestlerInRed;
        sf.Version++; p.CompleteMatch(sf, null, MatchWinTypeEnum.MutualDisqualify);
        sf.Version++; p.CompleteMatch(sf, null, MatchWinTypeEnum.MutualNoShow);
        var importer = new TournamentImporter(null, new List<IGroupBracketProcessor> { new SubGroupsToOlympicBracketProcessor() }, null);

        importer.Apply(peer, ImportPlan.Proceed(Clone(t)));

        var peerGroup = peer.Groups.Single();
        State(peerGroup).Should().Be(State(g));
        peerGroup.Bracket.Rounds.SelectMany(r => r.RoundMatches).Single(m => m.BracketFullNumber == sf.BracketFullNumber)
            .PreviousResults.Should().HaveCount(2);
        p.ClearWrestlerDisqualify(original);
        importer.Apply(peer, ImportPlan.Proceed(Clone(t)));
        State(peerGroup).Should().Be(State(g));
        peerGroup.Bracket.Rounds.SelectMany(r => r.RoundMatches).Single(m => m.BracketFullNumber == sf.BracketFullNumber)
            .PreviousResults.Should().BeEmpty();
        peer.Wrestlers.Should().OnlyContain(w => !w.IsPlaceless);
        Wrestling.Providers.Network.PeerStateHasher.Compute(Clone(peer)).Should()
            .Be(Wrestling.Providers.Network.PeerStateHasher.Compute(Clone(t)));
    }
}
