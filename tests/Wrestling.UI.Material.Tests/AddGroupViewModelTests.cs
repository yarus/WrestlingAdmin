using System;
using System.Collections.Generic;
using FluentAssertions;
using Wrestling.Entities;
using Wrestling.UI.Material.Tests.Fakes;
using Wrestling.UI.Material.Tournament.Standing.Details;
using Xunit;

namespace Wrestling.UI.Material.Tests;

public class AddGroupViewModelTests
{
    private static AgeWeightGroup NewGroup() => new()
    {
        ID = Guid.NewGuid(),
        BirthYearMin = 2005, BirthYearMax = 2006, WeightMax = 60,
        MaxRoundSecond = 180, MaxTimeoutSecond = 30, MaxActionSecond = 30
    };

    [Fact]
    public void IsFemaleT_setter_marks_item_as_female_and_raises_IsFemaleF()
    {
        var di = TestContainerBuilder.MakeDefault();
        var group = NewGroup();
        var vm = new AddGroupViewModel(di, group);

        var raised = new List<string>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        vm.IsFemaleT = true;

        group.IsFemale.Should().BeTrue();
        raised.Should().Contain("IsFemaleF");
    }

    [Fact]
    public void IsFemaleF_setter_marks_item_as_male_and_raises_IsFemaleT()
    {
        var di = TestContainerBuilder.MakeDefault();
        var group = NewGroup();
        group.IsFemale = true;
        var vm = new AddGroupViewModel(di, group);

        var raised = new List<string>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        vm.IsFemaleF = true;

        group.IsFemale.Should().BeFalse();
        raised.Should().Contain("IsFemaleT");
    }

    [Fact]
    public void IsFemaleT_getter_mirrors_item_IsFemale()
    {
        var di = TestContainerBuilder.MakeDefault();
        var group = NewGroup();
        group.IsFemale = true;

        var vm = new AddGroupViewModel(di, group);

        vm.IsFemaleT.Should().BeTrue();
        vm.IsFemaleF.Should().BeFalse();
    }

    [Fact]
    public void Null_item_returns_safe_defaults_for_gender_flags()
    {
        var di = TestContainerBuilder.MakeDefault();
        var vm = new AddGroupViewModel(di, null);

        vm.IsFemaleF.Should().Be(false);
        vm.IsFemaleT.Should().Be(false);
    }
}
