using System;
using System.Collections.Generic;
using FluentAssertions;
using Wrestling.Entities;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Material.Tests.Fakes;
using Wrestling.UI.Material.Tournament.Standing.Applications;
using Xunit;

namespace Wrestling.UI.Material.Tests;

public sealed class AddWrestlerSearchTests
{
    private static (AddWrestlerViewModel Vm, Wrestler Draft, Wrestler Cached) Create(decimal? fee = 500)
    {
        var cached = new Wrestler
        {
            ID = Guid.NewGuid(), LastName = "Иванов", FirstName = "Иван", MiddleName = "Петрович",
            BirthDate = new DateTime(2012, 3, 4), Weight = 44, Level = WrestlerLevelEnum.Junior1,
            IsWeightApproved = true, IsEntryFeePaid = true, PaidAmount = 100, HashTag = "athlete-123"
        };
        var draft = new Wrestler
        {
            ID = Guid.NewGuid(), LastName = "Иваненко", FirstName = "Сергей",
            BirthDate = new DateTime(2013, 5, 6), Weight = 42, PaidAmount = fee,
            TeamID = Guid.NewGuid(), Level = WrestlerLevelEnum.Junior2
        };
        var di = new TestDiContainer();
        di.Add<IDataContext>(new DataContext
        {
            Tournament = new Wrestling.Entities.Tournament(new GlobalSettings()) { EntryFee = fee },
            WrestlersCache = new List<Wrestler> { cached }
        });
        var vm = new AddWrestlerViewModel(di, draft);
        vm.InitData();
        return (vm, draft, cached);
    }

    [Fact]
    public void Typing_a_matching_name_and_browsing_suggestions_does_not_replace_the_draft()
    {
        var (vm, draft, cached) = Create();
        var original = (Wrestler)draft.Clone();

        vm.SearchText = "Ива";
        vm.FilteredAthletes.Should().ContainSingle().Which.Should().BeSameAs(cached);
        vm.SelectedItem.Should().BeNull();
        vm.SelectedItem = cached; // Keyboard navigation highlights a row without confirming it.

        draft.Should().BeEquivalentTo(original);
        vm.SearchText.Should().Be("Ива");
    }

    [Fact]
    public void Searching_for_an_unknown_athlete_and_clearing_the_query_keeps_manual_fields()
    {
        var (vm, draft, _) = Create();
        var original = (Wrestler)draft.Clone();
        vm.SearchText = "Новый спортсмен";
        vm.FilteredAthletes.Should().BeEmpty();
        vm.IsAthleteSearchOpen.Should().BeFalse();
        vm.SearchText = string.Empty;

        draft.Should().BeEquivalentTo(original);
        vm.SelectAthleteCommand.CanExecute(null).Should().BeFalse();
    }

    [Theory]
    [InlineData(500)]
    [InlineData(null)]
    public void Explicit_confirmation_fills_the_draft_and_resets_tournament_approvals(int? fee)
    {
        var (vm, draft, cached) = Create(fee);
        vm.SearchText = "Ива";
        vm.SelectedItem = cached;

        vm.SelectAthleteCommand.Execute(null);

        draft.ID.Should().Be(cached.ID);
        draft.FirstName.Should().Be("Иван");
        draft.BirthDate.Should().Be(cached.BirthDate);
        draft.Level.Should().Be(WrestlerLevelEnum.Junior1);
        draft.IsWeightApproved.Should().BeFalse();
        draft.IsEntryFeePaid.Should().Be(!fee.HasValue);
        draft.PaidAmount.Should().Be(fee ?? 0);
        cached.IsWeightApproved.Should().BeTrue();
        cached.PaidAmount.Should().Be(100);
        vm.SearchText.Should().BeEmpty();
        vm.SelectedItem.Should().BeNull();
        vm.IsAthleteSearchOpen.Should().BeFalse();
    }

    [Fact]
    public void Editing_the_query_clears_the_highlight_so_enter_cannot_apply_an_old_match()
    {
        var (vm, draft, cached) = Create();
        var original = (Wrestler)draft.Clone();
        vm.SearchText = "Ива";
        vm.SelectedItem = cached;
        vm.SearchText = "Другой";

        vm.SelectedItem.Should().BeNull();
        vm.SelectAthleteCommand.CanExecute(null).Should().BeFalse();
        draft.Should().BeEquivalentTo(original);
    }

    [Theory]
    [InlineData("Ив", 0)]
    [InlineData("  иваНОВ ив  ", 1)]
    [InlineData("петрович", 1)]
    [InlineData("athlete-123", 1)]
    public void Search_filters_names_and_identifiers_without_selecting(string query, int count)
    {
        var (vm, _, _) = Create();
        vm.SearchText = query;
        vm.FilteredAthletes.Should().HaveCount(count);
        vm.SelectedItem.Should().BeNull();
    }
}
