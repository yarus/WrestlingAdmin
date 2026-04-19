using System;
using System.Collections.Generic;
using System.ComponentModel;
using FluentAssertions;
using Wrestling.Entities;
using Xunit;

namespace Wrestling.Entities.Tests;

public class WrestlerTests
{
    [Fact]
    public void IsApplicationValid_requires_last_first_birth_and_group()
    {
        var w = new Wrestler();
        w.IsApplicationValid.Should().BeFalse();

        w.LastName = "Иванов";
        w.FirstName = "Иван";
        w.BirthDate = new DateTime(2005, 1, 1);
        w.IsApplicationValid.Should().BeFalse("GroupID is still null");

        w.GroupID = Guid.NewGuid();
        w.IsApplicationValid.Should().BeTrue();
    }

    [Fact]
    public void IsRegistrationApproved_requires_weight_fee_paid_and_weight_approved()
    {
        var w = TestHelpers.MakeWrestler(groupId: Guid.NewGuid());
        w.IsRegistrationApproved.Should().BeTrue();

        w.IsEntryFeePaid = false;
        w.IsRegistrationApproved.Should().BeFalse();
        w.IsEntryFeePaid = true;

        w.IsWeightApproved = false;
        w.IsRegistrationApproved.Should().BeFalse();
        w.IsWeightApproved = true;

        w.Weight = null;
        w.IsRegistrationApproved.Should().BeFalse();
    }

    [Fact]
    public void FullName_and_short_variants_are_built_from_parts()
    {
        var w = new Wrestler { LastName = "Иванов", FirstName = "Иван", MiddleName = "Иванович" };
        w.FullName.Should().Be("Иванов Иван Иванович");
        w.FullNameShort.Should().Be("Иванов И. И.");
        w.LastFirstName.Should().Be("Иванов Иван");
        w.LastFirstNameShort.Should().Be("Иванов И.");
    }

    [Fact]
    public void Clone_produces_value_copy_via_Sync()
    {
        var original = TestHelpers.MakeWrestler();
        original.PaidAmount = 500m;
        original.FinalPlace = 1;

        var clone = (Wrestler)original.Clone();

        clone.Should().NotBeSameAs(original);
        clone.ID.Should().Be(original.ID);
        clone.LastName.Should().Be(original.LastName);
        clone.FirstName.Should().Be(original.FirstName);
        clone.PaidAmount.Should().Be(500m);
        clone.FinalPlace.Should().Be(1);
    }

    [Fact]
    public void IsFemaleLabel_maps_to_letters()
    {
        new Wrestler { IsFemale = false }.IsFemaleLabel.Should().Be("М");
        new Wrestler { IsFemale = true }.IsFemaleLabel.Should().Be("Ж");
    }

    [Fact]
    public void Setting_BirthDate_raises_PropertyChanged_for_IsApplicationValid()
    {
        var w = TestHelpers.MakeWrestler(groupId: Guid.NewGuid());
        var raised = new List<string>();
        w.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        w.BirthDate = new DateTime(2008, 5, 5);

        raised.Should().Contain("IsApplicationValid");
    }
}
