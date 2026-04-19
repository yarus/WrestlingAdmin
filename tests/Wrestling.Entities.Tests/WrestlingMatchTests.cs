using System.Collections.Generic;
using FluentAssertions;
using Wrestling.Entities;
using Xunit;

namespace Wrestling.Entities.Tests;

public class WrestlingMatchTests
{
    [Fact]
    public void BracketFullNumber_combines_round_and_bracket_number()
    {
        var m = new WrestlingMatch { RoundNumber = 2, BracketNumber = 3 };
        m.BracketFullNumber.Should().Be("2.3");
    }

    [Fact]
    public void IsMatchCanStart_requires_both_wrestlers_and_not_completed()
    {
        var m = new WrestlingMatch();
        m.IsMatchCanStart.Should().BeFalse("no wrestlers set");

        m.WrestlerInRed = TestHelpers.MakeWrestler("Red");
        m.IsMatchCanStart.Should().BeFalse("blue missing");

        m.WrestlerInBlue = TestHelpers.MakeWrestler("Blue");
        m.IsMatchCanStart.Should().BeTrue();

        m.Status = MatchStatusEnum.Completed;
        m.IsMatchCanStart.Should().BeFalse("completed cannot start");
    }

    [Fact]
    public void MatchResult_shows_score_when_completed()
    {
        var m = new WrestlingMatch { PointsRed = 3, PointsBlue = 1 };
        m.MatchResult.Should().BeEmpty("not completed yet");

        m.Status = MatchStatusEnum.Completed;
        m.MatchResult.Should().Be("3 : 1");
    }

    [Fact]
    public void IsBlueWon_returns_inverse_of_IsRedWon()
    {
        var m = new WrestlingMatch();
        m.IsBlueWon.Should().BeFalse("IsRedWon is null -> default false");

        m.IsRedWon = true;
        m.IsBlueWon.Should().BeFalse();

        m.IsRedWon = false;
        m.IsBlueWon.Should().BeTrue();
    }

    // Bug-driver: setting IsBlueWon should fire PropertyChanged for both
    // IsBlueWon AND IsRedWon (the underlying flag it mutates), otherwise
    // bindings to IsRedWon do not refresh.
    [Fact]
    public void Setting_IsBlueWon_raises_PropertyChanged_for_IsRedWon_too()
    {
        var m = new WrestlingMatch { IsRedWon = true };
        var raised = new List<string>();
        m.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        m.IsBlueWon = true;

        raised.Should().Contain("IsBlueWon");
        raised.Should().Contain("IsRedWon",
            "setting IsBlueWon mutates _isRedWon internally; both listeners must be notified");
    }
}
