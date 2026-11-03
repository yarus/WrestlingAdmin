using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using FluentAssertions;
using Wrestling.Entities;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Material.Tests.Fakes;
using Wrestling.UI.Material.Tournament.Progress.Schedule;
using Wrestling.UI.Material.Tournament.Standing.Applications;
using Wrestling.UI.Material.Utils.Converters;
using Xunit;
using WTournament = Wrestling.Entities.Tournament;

namespace Wrestling.UI.Material.Tests;

public sealed class CrashRiskAuditTests
{
    [Fact]
    public void Add_team_handles_duplicate_registered_hashtags_and_still_filters_registered_teams()
    {
        var di = TestContainerBuilder.MakeDefault();
        var context = di.Resolve<IDataContext>();
        context.Tournament = new WTournament(new GlobalSettings());
        context.Tournament.TeamApplications.Add(new TeamApplication { HashTag = "yarigin-2026-1", ShortName = "Первая" });
        context.Tournament.TeamApplications.Add(new TeamApplication { HashTag = "yarigin-2026-1", ShortName = "Вторая" });
        var vm = new AddAppViewModel(di, new TeamApplication());
        vm.InitData();
        vm.TeamFilter("yarigin", new TeamApplication { HashTag = "yarigin-2026-1" }).Should().BeFalse();
        vm.TeamFilter("yarigin", new TeamApplication { HashTag = "yarigin-2026-2" }).Should().BeTrue();
        context.Tournament.TeamApplications.Clear();
        vm.InitData();
        vm.TeamFilter("yarigin", new TeamApplication { HashTag = "yarigin-2026-1" }).Should().BeTrue();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Schedule_revalidates_selected_and_expanded_mats_after_removal_or_replacing_tournament(bool remove)
    {
        var di = TestContainerBuilder.MakeDefault();
        var context = di.Resolve<IDataContext>();
        context.Tournament = new WTournament(new GlobalSettings());
        var first = new Mat { ID = Guid.NewGuid(), Name = "Ковёр A" };
        var second = new Mat { ID = Guid.NewGuid(), Name = "Ковёр B" };
        context.Tournament.Mats.Add(first);
        context.Tournament.Mats.Add(second);
        foreach (var mat in new[] { first, second })
        {
            var group = new AgeWeightGroup { MatID = mat.ID, Bracket = new GroupBracket() };
            var round = new GroupRound();
            round.RoundMatches.Add(new WrestlingMatch { GroupID = group.ID });
            group.Bracket.Rounds.Add(round);
            mat.Groups.Add(group);
            context.Tournament.Groups.Add(group);
        }
        var vm = new ScheduleViewModel(di);
        vm.InitData();
        vm.SelectedMat = vm.Stats[0];
        vm.SelectedMat.IsExpanded = true;
        if (remove) context.Tournament.Mats.Remove(first);
        else
        {
            context.Tournament = new WTournament(new GlobalSettings());
            context.Tournament.Mats.Add(second);
            context.Tournament.Mats.Add(new Mat { ID = Guid.NewGuid(), Name = "Ковёр C" });
        }
        vm.InitData();
        vm.SelectedMat.MatID.Should().Be(second.ID.Value);
        context.Tournament.Mats.Clear();
        vm.InitData();
        vm.SelectedMat.Should().BeNull();
        vm.Stats.Should().BeEmpty();
    }

    [Fact]
    public Task Corrupt_and_disappearing_emblems_fall_back_without_leaving_files_locked() => OnSta(() =>
    {
        var converter = new PathToImageConverter();
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".png");
        try
        {
            File.WriteAllText(path, "not an image");
            var fallback = (BitmapSource)converter.Convert(path, typeof(BitmapSource), null, CultureInfo.InvariantCulture);
            fallback.PixelWidth.Should().BeGreaterThan(0);
            File.Copy(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images", "DefaultLogo.png"), path, true);
            var valid = (BitmapSource)converter.Convert(path, typeof(BitmapSource), null, CultureInfo.InvariantCulture);
            valid.PixelWidth.Should().Be(fallback.PixelWidth);
            valid.IsFrozen.Should().BeTrue();
            using (File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
            File.Delete(path);
            converter.Convert(path, typeof(BitmapSource), null, CultureInfo.InvariantCulture).Should().BeOfType<BitmapImage>();
        }
        finally { File.Delete(path); }
    });

    private static Task OnSta(Action action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { action(); completion.TrySetResult(); }
            catch (Exception ex) { completion.TrySetException(ex); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task.WaitAsync(TimeSpan.FromSeconds(30));
    }
}
