using System;
using System.Collections.Generic;
using System.Reflection;
using FluentAssertions;
using Wrestling.Entities;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Material.Slider;
using Wrestling.UI.Material.Slider.Slides;
using Wrestling.UI.Material.Slider.Slides.UpcomingMatchesSlide;
using Wrestling.UI.Material.Tests.Fakes;
using Xunit;
using WTournament = Wrestling.Entities.Tournament;

namespace Wrestling.UI.Material.Tests;

public sealed class UpcomingMatchesSlideTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Categories_without_brackets_are_skipped(bool selectMat)
    {
        var di = TestContainerBuilder.MakeDefault();
        var tournament = new WTournament(new GlobalSettings());
        di.Resolve<IDataContext>().Tournament = tournament;
        var mat = new Mat { ID = Guid.NewGuid(), Name = "C" };
        tournament.Mats.Add(mat);
        var unseeded = new AgeWeightGroup();
        tournament.Groups.Add(unseeded);
        mat.Groups.Add(unseeded);
        var ready = ReadyMatch(2);
        var completed = ReadyMatch(1);
        completed.Status = MatchStatusEnum.Completed;
        var group = new AgeWeightGroup { Bracket = new GroupBracket() };
        group.Bracket.Rounds.Add(new GroupRound { RoundMatches = new List<WrestlingMatch> { completed, ready } });
        tournament.Groups.Add(group);
        mat.Groups.Add(group);
        var slide = new ScreenSlide();
        if (selectMat) slide.SetNamedValue("MatID", mat.ID);

        var vm = new UpcomingMatchesViewModel(di);
        vm.InitContext(slide);

        vm.UpcomingMatches.Should().Equal(ready);
    }

    [Fact]
    public void Mat_with_only_unseeded_categories_shows_no_matches()
    {
        var di = TestContainerBuilder.MakeDefault();
        var tournament = new WTournament(new GlobalSettings());
        di.Resolve<IDataContext>().Tournament = tournament;
        var mat = new Mat { ID = Guid.NewGuid(), Name = "C" };
        mat.Groups.Add(new AgeWeightGroup());
        tournament.Mats.Add(mat);
        var slide = new ScreenSlide();
        slide.SetNamedValue("MatID", mat.ID);

        var vm = new UpcomingMatchesViewModel(di);
        vm.InitContext(slide);

        vm.UpcomingMatches.Should().BeEmpty();
    }

    [Fact]
    public void Timer_can_rotate_to_mat_C_with_an_unseeded_category()
    {
        var di = TestContainerBuilder.MakeDefault();
        var tournament = new WTournament(new GlobalSettings());
        di.Resolve<IDataContext>().Tournament = tournament;
        var type = new UpcomingMatchesSlide(di);
        di.Add<List<ISlideType>>(new List<ISlideType> { type });
        var channel = new SlideChannel();
        foreach (var name in new[] { "A", "C" })
        {
            var mat = new Mat { ID = Guid.NewGuid(), Name = name };
            mat.Groups.Add(new AgeWeightGroup { Bracket = name == "A" ? new GroupBracket() : null });
            tournament.Mats.Add(mat);
            var slide = new ScreenSlide { SlideType = type.SlideType, Duration = 1 };
            slide.SetNamedValue("MatID", mat.ID);
            channel.Slides.Add(slide);
        }
        var host = new SlideHostViewModel(di) { Channel = channel };
        host.InitData();

        // Invoke the real timer handler without starting a Dispatcher loop.
        var tick = typeof(SlideHostViewModel).GetMethod("OnTimerTick", BindingFlags.Instance | BindingFlags.NonPublic);
        tick.Invoke(host, new object[] { null, EventArgs.Empty });

        host.CurrentSlide.Should().BeSameAs(channel.Slides[1]);
        var view = host.CurrentView.Should().BeOfType<UpcomingMatchesViewModel>().Subject;
        view.MatName.Should().Be("C");
        view.UpcomingMatches.Should().BeEmpty();

        tick.Invoke(host, new object[] { null, EventArgs.Empty });
        host.CurrentSlide.Should().BeSameAs(channel.Slides[0]);
    }

    private static WrestlingMatch ReadyMatch(int number) => new WrestlingMatch
    {
        MatchNumber = number,
        WrestlerInRed = new Wrestler(),
        WrestlerInBlue = new Wrestler()
    };
}
