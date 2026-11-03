using System;
using System.Linq;
using FluentAssertions;
using Wrestling.Entities.Bracket;
using Xunit;

namespace Wrestling.Entities.Tests;

public class ProcessorFactoryTests
{
    [Fact]
    public void Operations_and_format_options_never_share_instances()
    {
        var factory = new GroupBracketProcessorFactory();
        foreach (var option in factory.CreateAll())
        {
            var a = factory.Create(option.Code);
            var b = factory.Create(option.Code);
            a.Should().NotBeSameAs(b).And.NotBeSameAs(option);
            a.GetType().Should().Be(b.GetType());
        }
        factory.Create("unknown").Should().BeNull();
    }

    [Fact]
    public void A_foreign_or_replaced_match_is_rejected_before_any_result_changes()
    {
        var a = TestHelpers.MakeGroup(8);
        var b = TestHelpers.MakeGroup(8);
        var tournament = TestHelpers.MakeTournament(a, b);
        var processor = new OlympicWithConsolationFromFinalistsGroupBracketProcessor();
        processor.Generate(tournament, a);
        var match = a.Bracket.MainRounds().First().RoundMatches.First();
        processor.Generate(tournament, b);
        Action complete = () => processor.CompleteMatch(match, true, MatchWinTypeEnum.PointsWin);
        complete.Should().Throw<BracketStateException>();
        match.Status.Should().Be(MatchStatusEnum.Pending);
        match.WinType.Should().BeNull();
        processor.Generate(tournament, a);
        complete.Should().Throw<BracketStateException>();
        match.Status.Should().Be(MatchStatusEnum.Pending);
    }
}
