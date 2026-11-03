using System.Linq;
using FluentAssertions;
using Wrestling.Entities.Bracket;
using Xunit;

namespace Wrestling.Entities.Tests;

public class InvalidFreeWinRevertTests
{
    [Theory]
    [InlineData(5, false, false)]
    [InlineData(8, true, false)]
    [InlineData(5, false, true)]
    [InlineData(8, true, true)]
    public void A_full_pair_marked_as_free_win_can_be_corrected_but_a_real_bye_cannot(int count, bool canRevert, bool consolation)
    {
        var group = TestHelpers.MakeGroup(count);
        var tournament = TestHelpers.MakeTournament(group);
        GroupBracketProcessorBase processor = consolation
            ? new OlympicWithConsolationFromFinalistsGroupBracketProcessor()
            : new OlympicGroupBracketProcessor();
        processor.Generate(tournament, group);
        var match = group.Bracket.MainRounds().First().RoundMatches.First();
        if (count == 8) processor.CompleteMatch(match, true, MatchWinTypeEnum.FreeWin);

        processor.CanMatchBeReverted(match).Should().Be(canRevert);
        if (!canRevert) return;
        processor.RevertMatch(match);
        match.Status.Should().Be(MatchStatusEnum.Pending);
        match.WinType.Should().BeNull();
        match.WrestlerInRed.Should().NotBeNull();
        match.WrestlerInBlue.Should().NotBeNull();
        var next = group.Bracket.Rounds.SelectMany(r => r.RoundMatches).Single(m => m.BracketFullNumber == match.NextMatchBracketFullNumber);
        next.WrestlerInRed.Should().BeNull();
    }
}
