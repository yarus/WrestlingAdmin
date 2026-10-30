using System;
using FluentAssertions;
using Wrestling.Entities;
using Xunit;

namespace Wrestling.Entities.Tests;

public sealed class WrestlerRegistrationTests
{
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void Adding_registration_invalidates_only_its_target_group(bool paid, bool weighed)
    {
        var tournament = new Tournament(new GlobalSettings());
        var target = new AgeWeightGroup { ID = Guid.NewGuid(), Bracket = new GroupBracket(), BracketVersion = 7 };
        var other = new AgeWeightGroup { ID = Guid.NewGuid(), Bracket = new GroupBracket(), BracketVersion = 3 };
        var otherBracket = other.Bracket;
        var member = new Wrestler { ID = Guid.NewGuid(), FinalPlace = 1, SeedNumber = 2, IsSeedFixed = true };
        target.Wrestlers.Add(member);
        tournament.Groups.Add(target);
        tournament.Groups.Add(other);
        var entrant = new Wrestler
        {
            ID = Guid.NewGuid(), GroupID = target.ID,
            LastName = "Иванов", FirstName = "Иван", BirthDate = new DateTime(2013, 4, 5), Weight = 43,
            IsEntryFeePaid = paid, IsWeightApproved = weighed
        };

        tournament.AddWrestlerRegistration(entrant).Should().BeTrue();

        tournament.Wrestlers.Should().ContainSingle().Which.Should().BeSameAs(entrant);
        target.Bracket.Should().BeNull();
        target.BracketVersion.Should().Be(8);
        entrant.GroupName.Should().Be(target.Name);
        target.Wrestlers.Contains(entrant).Should().Be(paid && weighed);
        member.FinalPlace.Should().BeNull();
        member.SeedNumber.Should().Be(2);
        member.IsSeedFixed.Should().BeTrue();
        other.Bracket.Should().BeSameAs(otherBracket);
        other.BracketVersion.Should().Be(3);
    }

    [Fact]
    public void Adding_without_a_group_preserves_existing_draws()
    {
        var tournament = new Tournament(new GlobalSettings());
        var group = new AgeWeightGroup { ID = Guid.NewGuid(), Bracket = new GroupBracket(), BracketVersion = 4 };
        var bracket = group.Bracket;
        tournament.Groups.Add(group);
        var entrant = new Wrestler { ID = Guid.NewGuid() };

        tournament.AddWrestlerRegistration(entrant).Should().BeFalse();

        tournament.Wrestlers.Should().ContainSingle().Which.Should().BeSameAs(entrant);
        group.Bracket.Should().BeSameAs(bracket);
        group.BracketVersion.Should().Be(4);
    }
}
