using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Wrestling.Entities;
using Wrestling.Providers;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Material.Slider;
using Wrestling.UI.Material.Tournament.Progress.Schedule;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Tournament.Conducting
{
    // «Проведение» — admin landing page. Central pane is an aggregator
    // dashboard (tournament progress, per-carpet status, last 3 results).
    // Sidebar carries navigation cards (Schedule, Slider) and peer-sync status.
    public class ConductingViewModel : TournamentViewModelBase
    {
        private PeerSyncStatusTracker _tracker;
        private IResultsService _resultsService;
        private bool _resultsSubscribed;

        private ICommand _openScheduleCommand;
        private ICommand _openSliderCommand;

        public ConductingViewModel(IDiContainer container) : base(container)
        {
            LastThreeResults = new ObservableCollection<RecentMatchSummary>();
        }

        public override string PageTitle => "Проведение";

        public override bool IsBackButtonAvailable => false;

        public ObservableCollection<PeerStatusViewModel> PeerStatuses => _tracker?.Peers;

        public ObservableCollection<RecentMatchSummary> LastThreeResults { get; }

        public bool HasBrackets => DataContext?.Tournament != null && DataContext.Tournament.MatchesCount > 0;

        public bool HasPendingCarpets =>
            DataContext?.Tournament != null && DataContext.Tournament.Carpets.Any(c => c.MatchesCount > 0);

        public bool HasRecentResults => LastThreeResults.Count > 0;

        public IEnumerable<Carpet> PendingCarpets =>
            DataContext?.Tournament?.Carpets?.Where(c => c.MatchesCount > 0) ?? Enumerable.Empty<Carpet>();

        public string TournamentDurationLabel
        {
            get
            {
                var seconds = DataContext?.Tournament?.ExpectedDurationInSeconds ?? 0;
                var ts = TimeSpan.FromSeconds(seconds);
                if ((int)ts.TotalHours >= 1)
                {
                    return $"{(int)ts.TotalHours}ч {ts.Minutes:D2}мин";
                }
                return $"{ts.Minutes}мин";
            }
        }

        public ICommand OpenScheduleCommand =>
            _openScheduleCommand ?? (_openScheduleCommand = new RelayCommand(
                _ => NavigateToView<ScheduleViewModel>(),
                _ => true));

        public ICommand OpenSliderCommand =>
            _openSliderCommand ?? (_openSliderCommand = new RelayCommand(
                _ => NavigateToView<SliderControlViewModel>(),
                _ => true));

        public override void InitData()
        {
            base.InitData();

            if (_tracker == null)
            {
                _tracker = Resolve<PeerSyncStatusTracker>();
                OnPropertyChanged(nameof(PeerStatuses));
            }

            if (_resultsService == null)
            {
                _resultsService = Resolve<IResultsService>();
            }

            if (_resultsService != null && !_resultsSubscribed)
            {
                _resultsService.ResultsChanged += OnResultsChanged;
                _resultsSubscribed = true;
            }

            RefreshAll();
        }

        public override void OnNavigationCompleted()
        {
            base.OnNavigationCompleted();
            RefreshAll();
        }

        private void OnResultsChanged()
        {
            RefreshAll();
        }

        private void RefreshAll()
        {
            var tournament = DataContext?.Tournament;
            if (tournament == null)
            {
                LastThreeResults.Clear();
                OnPropertyChanged(nameof(HasBrackets));
                OnPropertyChanged(nameof(HasPendingCarpets));
                OnPropertyChanged(nameof(HasRecentResults));
                OnPropertyChanged(nameof(PendingCarpets));
                OnPropertyChanged(nameof(TournamentDurationLabel));
                return;
            }

            RebuildLastThreeResults(tournament);
            tournament.RefreshAggregates();
            foreach (var carpet in tournament.Carpets)
            {
                carpet.RefreshStats();
            }

            OnPropertyChanged(nameof(HasBrackets));
            OnPropertyChanged(nameof(HasPendingCarpets));
            OnPropertyChanged(nameof(HasRecentResults));
            OnPropertyChanged(nameof(PendingCarpets));
            OnPropertyChanged(nameof(TournamentDurationLabel));
        }

        private void RebuildLastThreeResults(Entities.Tournament tournament)
        {
            var carpetByGroup = new Dictionary<Guid, Carpet>();
            foreach (var carpet in tournament.Carpets)
            {
                foreach (var group in carpet.Groups)
                {
                    carpetByGroup[group.ID] = carpet;
                }
            }

            var top3 = tournament.Groups
                .Where(g => g.Bracket != null)
                .SelectMany(g => g.Bracket.Rounds.SelectMany(r => r.RoundMatches),
                    (g, m) => new { Group = g, Match = m })
                .Where(x => x.Match.Status == MatchStatusEnum.Completed && x.Match.StartDateTime.HasValue)
                .OrderByDescending(x => x.Match.StartDateTime.Value)
                .Take(3)
                .Select(x => BuildSummary(x.Group, x.Match, carpetByGroup))
                .ToList();

            LastThreeResults.Clear();
            foreach (var summary in top3)
            {
                LastThreeResults.Add(summary);
            }
        }

        private static RecentMatchSummary BuildSummary(
            AgeWeightGroup group,
            WrestlingMatch match,
            IReadOnlyDictionary<Guid, Carpet> carpetByGroup)
        {
            var time = match.StartDateTime?.ToString("HH:mm") ?? string.Empty;
            var carpetName = carpetByGroup.TryGetValue(group.ID, out var carpet) ? carpet.Name : string.Empty;
            var weightLabel = group.Name ?? string.Empty;

            var redName = match.WrestlerInRed?.LastFirstNameShort ?? "—";
            var blueName = match.WrestlerInBlue?.LastFirstNameShort ?? "—";
            var pair = $"{redName} — {blueName}";

            string resultLine;
            if (match.WrestlerInRed == null || match.WrestlerInBlue == null)
            {
                // Auto-completed (FreeWin) — only one wrestler is set.
                var loneWinner = match.WrestlerInRed?.LastFirstNameShort ?? match.WrestlerInBlue?.LastFirstNameShort ?? "—";
                resultLine = $"Победа: {loneWinner}, {ShortWinType(match.WinType)}";
            }
            else
            {
                var winner = match.IsRedWon == true ? redName : blueName;
                var score = $"{match.PointsRed}:{match.PointsBlue}";
                resultLine = $"Победа: {winner}, {ShortWinType(match.WinType)} {score}";
            }

            return new RecentMatchSummary
            {
                Time = time,
                CarpetName = carpetName,
                WeightLabel = weightLabel,
                Pair = pair,
                ResultLine = resultLine
            };
        }

        // Short wrestling notation codes used by referees worldwide. Mapped from
        // MatchWinTypeEnum so the "last 3 results" feed reads as a glance-able
        // single line; the long-form WinTypeToStringConverter is too verbose here.
        private static string ShortWinType(MatchWinTypeEnum? winType)
        {
            if (!winType.HasValue) return string.Empty;
            switch (winType.Value)
            {
                case MatchWinTypeEnum.Tushe: return "VFA";
                case MatchWinTypeEnum.Injury: return "VIN";
                case MatchWinTypeEnum.WarningsLimit: return "VCA";
                case MatchWinTypeEnum.NoShow: return "VFO";
                case MatchWinTypeEnum.DisqualifyWin: return "DSQ";
                case MatchWinTypeEnum.DominationWin: return "VSU";
                case MatchWinTypeEnum.DominationWinWithPoints: return "VSU1";
                case MatchWinTypeEnum.PointsWin: return "VPO";
                case MatchWinTypeEnum.PointsWinWithPoints: return "VPO1";
                case MatchWinTypeEnum.ActionWin: return "VPO1";
                case MatchWinTypeEnum.FreeWin: return "BYE";
                default: return string.Empty;
            }
        }
    }

    public sealed class RecentMatchSummary
    {
        public string Time { get; set; }
        public string CarpetName { get; set; }
        public string WeightLabel { get; set; }
        public string Pair { get; set; }
        public string ResultLine { get; set; }
    }
}
