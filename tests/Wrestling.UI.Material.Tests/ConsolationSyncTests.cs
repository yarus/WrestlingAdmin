using System;
using System.Linq;
using FluentAssertions;
using Wrestling.Entities;
using Wrestling.Entities.Bracket;
using Wrestling.Providers;
using Wrestling.Providers.Network;
using Wrestling.UI.Material.Model;
using Xunit;

namespace Wrestling.UI.Material.Tests;

public class ConsolationSyncTests
{
    [Theory]
    [InlineData(16)]
    [InlineData(18)]
    [InlineData(32)]
    public void Derived_passes_and_real_results_converge_after_each_out_of_order_completion(int count)
    {
        var author = new Wrestling.Entities.Tournament(new GlobalSettings()) { ID = Guid.NewGuid() };
        var group = new AgeWeightGroup { ID = Guid.NewGuid() };
        author.Groups.Add(group);
        for (var i = 0; i < count; i++)
        {
            var wrestler = new Wrestler { ID = Guid.NewGuid(), GroupID = group.ID, SeedNumber = i + 1 };
            group.Wrestlers.Add(wrestler); author.Wrestlers.Add(wrestler);
        }
        var factory = new GroupBracketProcessorFactory();
        var adapter = new EntityToInfoAdapter();
        factory.Create(BracketTypeEnum.OlympicConsilationFinalists.ToString()).Generate(author, group);
        var peer = adapter.GetEntityFromInfo(adapter.GetInfoFromEntity(author));
        var importer = new TournamentImporter(null, factory, null);
        for (var step = 0; step < 200 && !group.IsBracketCompleted; step++)
        {
            var available = group.Bracket.Rounds.SelectMany(r => r.RoundMatches).Where(m => m.IsMatchCanStart).ToList();
            available.Should().NotBeEmpty();
            var match = step % 2 == 0 ? available.Last() : available.First();
            var processor = factory.Create(group.Bracket.BracketTypeCode);
            processor.Load(author, group);
            processor.CompleteMatch(match, step % 3 == 0, MatchWinTypeEnum.PointsWin);
            match.Version++;

            var snapshot = adapter.GetEntityFromInfo(adapter.GetInfoFromEntity(author));
            importer.Apply(peer, ImportPlan.Proceed(snapshot)).Outcome.Should().Be(ImportOutcome.Imported);
            PeerStateHasher.Compute(peer).Should().Be(PeerStateHasher.Compute(author));
            State(peer.Groups[0]).Should().Be(State(group));
            importer.Apply(peer, ImportPlan.Proceed(snapshot)).Outcome.Should().Be(ImportOutcome.NoNewData);
        }
        group.IsBracketCompleted.Should().BeTrue();
        peer.Groups[0].IsBracketCompleted.Should().BeTrue();
    }

    private static string State(AgeWeightGroup group) => string.Join(";", group.Bracket.Rounds.SelectMany(r => r.RoundMatches)
        .Select(m => $"{m.BracketFullNumber}:{m.Status}:{m.WinType}:{m.IsRedWon}:{m.WrestlerInRed?.ID}:{m.WrestlerInBlue?.ID}"));
}
