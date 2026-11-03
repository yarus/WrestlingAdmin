using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using PdfSharp.Pdf.IO;
using Wrestling.Entities;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Material.Tests.Fakes;
using Wrestling.UI.Material.Tournament.Print;
using Wrestling.UI.Material.Tournament.Print.PrintSchedule;
using Wrestling.UI.Material.Tournament.Standing.Mats;
using Xunit;
using WTournament = Wrestling.Entities.Tournament;

namespace Wrestling.UI.Material.Tests;

public sealed class PrintSchedulePartsTests
{
    private static (TestDiContainer Di, WTournament Tournament, Mat Mat, TournamentPart First, TournamentPart Second) Build()
    {
        var di = TestContainerBuilder.MakeDefault();
        var tournament = new WTournament(new GlobalSettings());
        di.Resolve<IDataContext>().Tournament = tournament;
        var first = new TournamentPart { ID = Guid.NewGuid(), Name = "Часть 1" };
        var second = new TournamentPart { ID = Guid.NewGuid(), Name = "Часть 2" };
        tournament.Parts.Add(first);
        tournament.Parts.Add(second);
        var mat = new Mat { ID = Guid.NewGuid(), Name = "Ковер A", ActivePartID = first.ID };
        tournament.Mats.Add(mat);
        AddGroup(tournament, mat, first);
        AddGroup(tournament, mat, second);
        return (di, tournament, mat, first, second);
    }

    private static AgeWeightGroup AddGroup(WTournament tournament, Mat mat, TournamentPart part)
    {
        var group = new AgeWeightGroup { ID = Guid.NewGuid(), MatID = mat.ID, PartID = part?.ID, Bracket = new GroupBracket() };
        var round = new GroupRound();
        round.RoundMatches.Add(new WrestlingMatch { MatchNumber = 2, GroupID = group.ID });
        round.RoundMatches.Add(new WrestlingMatch { MatchNumber = 1, GroupID = group.ID });
        round.RoundMatches.Add(new WrestlingMatch { MatchNumber = 3, GroupID = group.ID, Status = MatchStatusEnum.Completed });
        group.Bracket.Rounds.Add(round);
        tournament.Groups.Add(group);
        mat.Groups.Add(group);
        return group;
    }

    [Fact]
    public void Second_part_can_be_printed_while_the_first_is_active_and_unfinished()
    {
        var (di, tournament, mat, first, second) = Build();
        var vm = new PrintScheduleViewModel(di, mat, second);

        vm.InitData();

        vm.Stat.Matches.Select(m => m.GroupID).Should().OnlyContain(id => id == tournament.Groups[1].ID);
        vm.Stat.Matches.Select(m => m.MatchNumber).Should().Equal(1, 2);
        vm.Stat.GroupsCount.Should().Be(1);
        vm.MatPartLabel.Should().Contain(second.Name);
        mat.ActivePartID.Should().Be(first.ID);
        tournament.Groups[0].Bracket.Rounds[0].RoundMatches.Should().Contain(m => !m.IsMatchCompleted);
    }

    [Fact]
    public void All_parts_keep_part_order_when_match_numbers_restart()
    {
        var (di, tournament, mat, _, _) = Build();
        var vm = new PrintScheduleViewModel(di, mat);

        vm.InitData();

        vm.Stat.Matches.Select(m => m.GroupID).Should().Equal(
            tournament.Groups[0].ID, tournament.Groups[0].ID, tournament.Groups[1].ID, tournament.Groups[1].ID);
        vm.Stat.Matches.Select(m => m.MatchNumber).Should().Equal(1, 2, 1, 2);
    }

    [Fact]
    public void Selected_part_exports_only_mats_with_pending_matches_in_that_part()
    {
        var (di, tournament, _, first, second) = Build();
        var otherMat = new Mat { ID = Guid.NewGuid(), Name = "Ковер B", ActivePartID = first.ID };
        tournament.Mats.Add(otherMat);
        AddGroup(tournament, otherMat, first);
        var vm = new MatsViewModel(di);
        vm.InitData();

        var jobs = vm.BuildScheduleExportJobs(tournament, second);

        jobs.Should().ContainSingle();
        jobs[0].FileName.Should().Contain("Ковер A").And.Contain(second.Name);
        otherMat.ActivePartID.Should().Be(first.ID);
    }

    [Fact]
    public void All_parts_export_separate_unique_files_even_when_part_names_match()
    {
        var (di, tournament, _, first, second) = Build();
        second.Name = first.Name;
        var vm = new MatsViewModel(di);
        vm.InitData();

        var jobs = vm.BuildScheduleExportJobs(tournament, null);

        jobs.Should().HaveCount(2);
        jobs.Select(j => j.FileName).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Empty_or_completed_selected_part_does_not_create_a_blank_pdf()
    {
        var (di, tournament, _, _, second) = Build();
        foreach (var match in tournament.Groups[1].Bracket.Rounds.SelectMany(r => r.RoundMatches))
            match.Status = MatchStatusEnum.Completed;
        var vm = new MatsViewModel(di);
        vm.InitData();

        vm.BuildScheduleExportJobs(tournament, second).Should().BeEmpty();
        vm.BuildScheduleExportJobs(tournament, null).Should().ContainSingle();
    }

    [Fact]
    public void Legacy_tournament_without_parts_keeps_one_file_per_mat()
    {
        var di = TestContainerBuilder.MakeDefault();
        var tournament = new WTournament(new GlobalSettings());
        di.Resolve<IDataContext>().Tournament = tournament;
        var mat = new Mat { ID = Guid.NewGuid(), Name = "A" };
        tournament.Mats.Add(mat);
        AddGroup(tournament, mat, null);
        var vm = new MatsViewModel(di);
        vm.InitData();

        vm.BuildScheduleExportJobs(tournament, null).Should().ContainSingle().Which.FileName.Should().Be("Расписание_A.pdf");
    }

    [Fact]
    public async Task All_parts_render_to_separate_nonempty_PDFs()
    {
        var (di, tournament, mat, first, _) = Build();
        var vm = new MatsViewModel(di);
        vm.InitData();
        var jobs = vm.BuildScheduleExportJobs(tournament, null);
        var folder = Path.Combine(Path.GetTempPath(), "WrestlingScheduleParts-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var result = await new BulkBracketPdfExporter().ExportAsync(jobs, folder);

            result.Failures.Should().BeEmpty();
            result.Succeeded.Should().Be(2);
            foreach (var job in jobs)
            {
                var path = Path.Combine(folder, job.FileName);
                new FileInfo(path).Length.Should().BeGreaterThan(500);
                using var pdf = PdfReader.Open(path, PdfDocumentOpenMode.ReadOnly);
                pdf.PageCount.Should().BeGreaterThan(0);
            }
            mat.ActivePartID.Should().Be(first.ID);
        }
        finally { Directory.Delete(folder, recursive: true); }
    }

    [Fact]
    public void Selector_offers_all_parts_including_unstarted_second_part()
    {
        var (_, tournament, _, first, second) = Build();

        var vm = new PartSelectorDialogViewModel(tournament);

        vm.Options.Select(o => o.Part).Should().Equal(null, first, second);
    }
}
