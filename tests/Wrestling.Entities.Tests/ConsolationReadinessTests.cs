using System.Linq;
using FluentAssertions;
using Wrestling.Entities.Bracket;
using Xunit;

namespace Wrestling.Entities.Tests;

public class ConsolationReadinessTests
{
    [Theory]
    [InlineData(9)]
    [InlineData(16)]
    [InlineData(18)]
    [InlineData(32)]
    public void Additional_matches_wait_for_their_feeders_even_after_loading(int count)
    {
        var group = TestHelpers.MakeGroup(count);
        var tournament = TestHelpers.MakeTournament(group);
        var processor = new OlympicWithConsolationFromFinalistsGroupBracketProcessor();
        processor.Generate(tournament, group);
        foreach (var round in group.Bracket.MainRounds().ToList())
        foreach (var match in round.RoundMatches.Where(m => !m.IsMatchCompleted).ToList())
            processor.CompleteMatch(match, true, MatchWinTypeEnum.PointsWin);

        processor.Load(tournament, group);
        processor.GetResults().ToList();
        var additional = group.Bracket.AdditionalRounds().SelectMany(r => r.RoundMatches).ToList();
        foreach (var feeder in additional.Where(m => !m.IsMatchCompleted && !string.IsNullOrEmpty(m.NextMatchBracketFullNumber)))
            additional.Single(m => m.BracketFullNumber == feeder.NextMatchBracketFullNumber)
                .Status.Should().Be(MatchStatusEnum.Pending, "an unfinished consolation can still supply an opponent");

        foreach (var round in group.Bracket.AdditionalRounds().ToList())
        foreach (var match in round.RoundMatches.ToList())
        {
            if (!match.IsMatchCompleted)
            {
                match.WrestlerInRed.Should().NotBeNull();
                match.WrestlerInBlue.Should().NotBeNull();
                processor.CompleteMatch(match, true, MatchWinTypeEnum.PointsWin);
            }
        }
        additional.Should().NotContain(m => m.WinType == MatchWinTypeEnum.FreeWin && m.WrestlerInRed != null && m.WrestlerInBlue != null);
        group.Bracket.Rounds.SelectMany(r => r.RoundMatches).Should().OnlyContain(m => m.IsMatchCompleted);
    }
}
