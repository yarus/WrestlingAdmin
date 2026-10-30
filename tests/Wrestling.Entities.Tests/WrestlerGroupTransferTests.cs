using System;
using FluentAssertions;
using Wrestling.Entities;
using Xunit;

namespace Wrestling.Entities.Tests;

public sealed class WrestlerGroupTransferTests
{
    private static (Tournament Tournament, AgeWeightGroup Source, AgeWeightGroup Target,
        Wrestler Wrestler, Wrestler Edited) Create()
    {
        var tournament = new Tournament(new GlobalSettings());
        var source = new AgeWeightGroup { ID = Guid.NewGuid(), WeightMax = 41, Bracket = new GroupBracket(), BracketVersion = 3 };
        var target = new AgeWeightGroup { ID = Guid.NewGuid(), WeightMax = 44, Bracket = new GroupBracket(), BracketVersion = 7 };
        tournament.Groups.Add(source);
        tournament.Groups.Add(target);
        var wrestler = new Wrestler
        {
            ID = Guid.NewGuid(), LastName = "Иванов", FirstName = "Иван", BirthDate = new DateTime(2013, 4, 5),
            Weight = 40, GroupID = source.ID, IsSeedFixed = true, SeedNumber = 8,
            IsEntryFeePaid = true, IsWeightApproved = true, FinalPlace = 2
        };
        source.Wrestlers.Add(wrestler);
        tournament.Wrestlers.Add(wrestler);
        var edited = (Wrestler)wrestler.Clone();
        edited.Weight = 43;
        edited.GroupID = target.ID;
        return (tournament, source, target, wrestler, edited);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Moving_resets_seed_and_both_brackets_even_if_weigh_in_is_not_approved(bool approved)
    {
        var (tournament, source, target, wrestler, edited) = Create();
        edited.IsWeightApproved = approved;
        var targetMember = new Wrestler { ID = Guid.NewGuid(), FinalPlace = 1, SeedNumber = 2, IsSeedFixed = true };
        target.Wrestlers.Add(targetMember);

        tournament.UpdateWrestlerRegistration(wrestler, edited).Should().BeTrue();

        wrestler.Weight.Should().Be(43);
        wrestler.GroupID.Should().Be(target.ID);
        wrestler.GroupName.Should().Be(target.Name);
        wrestler.SeedNumber.Should().BeNull();
        wrestler.IsSeedFixed.Should().BeFalse();
        wrestler.FinalPlace.Should().BeNull();
        source.Wrestlers.Should().BeEmpty();
        source.Bracket.Should().BeNull();
        target.Bracket.Should().BeNull();
        source.BracketVersion.Should().Be(4);
        target.BracketVersion.Should().Be(8);
        target.Wrestlers.Contains(wrestler).Should().Be(approved);
        target.Wrestlers.Should().NotContain(w => ReferenceEquals(w, edited));
        targetMember.FinalPlace.Should().BeNull();
        targetMember.SeedNumber.Should().Be(2);
        targetMember.IsSeedFixed.Should().BeTrue();
    }

    [Fact]
    public void Target_group_uses_the_live_registration_and_observes_later_edits()
    {
        var (tournament, _, target, wrestler, edited) = Create();
        tournament.UpdateWrestlerRegistration(wrestler, edited);
        target.Wrestlers.Should().ContainSingle().Which.Should().BeSameAs(tournament.Wrestlers[0]);

        wrestler.Weight = 44;
        target.Wrestlers[0].Weight.Should().Be(44);
    }

    [Fact]
    public void Changing_weight_within_the_same_category_keeps_the_draw_and_fixed_seed()
    {
        var (tournament, source, target, wrestler, edited) = Create();
        edited.GroupID = source.ID;
        var sourceBracket = source.Bracket;
        var targetBracket = target.Bracket;

        tournament.UpdateWrestlerRegistration(wrestler, edited).Should().BeFalse();

        wrestler.Weight.Should().Be(43);
        wrestler.SeedNumber.Should().Be(8);
        wrestler.IsSeedFixed.Should().BeTrue();
        source.Bracket.Should().BeSameAs(sourceBracket);
        target.Bracket.Should().BeSameAs(targetBracket);
        source.BracketVersion.Should().Be(3);
        target.BracketVersion.Should().Be(7);
        source.Wrestlers.Should().ContainSingle().Which.Should().BeSameAs(wrestler);
    }

    [Fact]
    public void Moving_out_of_a_group_clears_its_draw_and_does_not_touch_other_groups()
    {
        var (tournament, source, target, wrestler, edited) = Create();
        edited.GroupID = null;
        var untouchedBracket = target.Bracket;

        tournament.UpdateWrestlerRegistration(wrestler, edited);

        wrestler.GroupID.Should().BeNull();
        wrestler.GroupName.Should().BeEmpty();
        wrestler.SeedNumber.Should().BeNull();
        wrestler.IsSeedFixed.Should().BeFalse();
        source.Bracket.Should().BeNull();
        source.Wrestlers.Should().BeEmpty();
        target.Bracket.Should().BeSameAs(untouchedBracket);
        target.BracketVersion.Should().Be(7);
    }

    [Fact]
    public void Assigning_an_unapproved_wrestler_to_a_group_also_invalidates_the_target_draw()
    {
        var (tournament, source, target, wrestler, edited) = Create();
        source.Wrestlers.Clear();
        wrestler.GroupID = null;
        edited.IsEntryFeePaid = false;
        var untouchedBracket = source.Bracket;

        tournament.UpdateWrestlerRegistration(wrestler, edited);

        wrestler.GroupID.Should().Be(target.ID);
        wrestler.IsRegistrationApproved.Should().BeFalse();
        wrestler.SeedNumber.Should().BeNull();
        target.Bracket.Should().BeNull();
        target.BracketVersion.Should().Be(8);
        source.Bracket.Should().BeSameAs(untouchedBracket);
    }
}
