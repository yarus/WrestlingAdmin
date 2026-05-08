using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Wrestling.Entities;
using Wrestling.Providers;
using Wrestling.Providers.Network;
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
        private ICommand _openCarpetScheduleCommand;
        private ICommand _openSliderCommand;

        public ConductingViewModel(IDiContainer container) : base(container)
        {
            RecentResults = new ObservableCollection<RecentMatchSummary>();
        }

        public override string PageTitle => "Проведение";

        public override bool IsBackButtonAvailable => false;

        public ObservableCollection<PeerStatusViewModel> PeerStatuses => _tracker?.Peers;

        public ObservableCollection<RecentMatchSummary> RecentResults { get; }

        public bool HasBrackets => DataContext?.Tournament != null && DataContext.Tournament.MatchesCount > 0;

        public bool HasPendingCarpets =>
            DataContext?.Tournament != null && DataContext.Tournament.Carpets.Any(c => c.MatchesCount > 0);

        public bool HasRecentResults => RecentResults.Count > 0;

        // Tournament-wide aggregates for the «Состав» info card on Conducting.
        // Refreshed in RefreshAll() on every navigation tick + results event.
        public int WrestlersCount => DataContext?.Tournament?.AppliedWrestlersCount ?? 0;

        public int TeamsCount => DataContext?.Tournament?.ApplicationsCount ?? 0;

        // Distinct city count across team applications. Trim + case-insensitive
        // so cosmetic variations of the same city don't double-count.
        public int RegionsCount
        {
            get
            {
                var teams = DataContext?.Tournament?.TeamApplications;
                if (teams == null) return 0;
                return teams
                    .Select(t => t.City?.Trim().ToLowerInvariant())
                    .Where(c => !string.IsNullOrEmpty(c))
                    .Distinct()
                    .Count();
            }
        }

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

        // Carpet card click on the dashboard — opens Schedule with the clicked
        // carpet pre-selected so the operator lands directly on that queue.
        public ICommand OpenCarpetScheduleCommand =>
            _openCarpetScheduleCommand ?? (_openCarpetScheduleCommand = new RelayCommand(
                param => OpenCarpetSchedule(param as Carpet),
                param => param is Carpet));

        private void OpenCarpetSchedule(Carpet carpet)
        {
            if (carpet?.ID.HasValue != true) return;
            // ScheduleViewModel lives in NavigationService's singleton list, not
            // DiContainer — Resolve<T> returns null. Navigation.GetViewModel<T>
            // is the correct lookup for nav-managed singletons.
            var schedule = Navigation.GetViewModel<ScheduleViewModel>();
            if (schedule != null)
            {
                schedule.PreselectedCarpetId = carpet.ID.Value;
            }
            NavigateToView<ScheduleViewModel>();
        }

        public ICommand OpenSliderCommand =>
            _openSliderCommand ?? (_openSliderCommand = new RelayCommand(
                _ => NavigateToView<SliderControlViewModel>(),
                _ => true));

        #region Network card

        // Tournament-level GlobalSettings — directly bound by the Сеть card
        // (NodeName, DiscoveryPort, IsHttpServerEnabled, HttpServerPort).
        // Conducting is only reachable when a tournament is open, so this is
        // never null in practice; the null-conditional defends against design-time.
        public GlobalSettings Settings => DataContext?.Tournament?.Settings;

        public const string AnnounceAuto = "(Авто)";

        // Hidden when the host has a single IP and no stale override — there's
        // nothing to choose. Mirrors the rule from the old Settings page.
        public bool IsAnnounceAddressPickerVisible
        {
            get
            {
                if (LocalIpAddressProbe.EnumerateLanAddresses().Count > 1) return true;
                return Settings != null && !string.IsNullOrEmpty(Settings.AnnounceIpOverride);
            }
        }

        public IList<string> AnnounceAddressOptions
        {
            get
            {
                var options = new List<string> { AnnounceAuto };
                foreach (var ip in LocalIpAddressProbe.EnumerateLanAddresses())
                {
                    options.Add(ip.ToString());
                }
                // Preserve a stale override even if the matching NIC is gone —
                // operators need to see (and clear) what's currently set.
                var saved = Settings?.AnnounceIpOverride;
                if (!string.IsNullOrWhiteSpace(saved) && !options.Contains(saved))
                {
                    options.Add(saved);
                }
                return options;
            }
        }

        public string SelectedAnnounceAddress
        {
            get
            {
                if (Settings == null) return AnnounceAuto;
                return string.IsNullOrEmpty(Settings.AnnounceIpOverride) ? AnnounceAuto : Settings.AnnounceIpOverride;
            }
            set
            {
                if (Settings == null) return;
                Settings.AnnounceIpOverride = (string.IsNullOrEmpty(value) || value == AnnounceAuto) ? string.Empty : value;
                OnPropertyChanged(nameof(SelectedAnnounceAddress));
                OnPropertyChanged(nameof(IsAnnounceAddressPickerVisible));
            }
        }

        #endregion

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
                RecentResults.Clear();
                OnPropertyChanged(nameof(HasBrackets));
                OnPropertyChanged(nameof(HasPendingCarpets));
                OnPropertyChanged(nameof(HasRecentResults));
                OnPropertyChanged(nameof(PendingCarpets));
                OnPropertyChanged(nameof(TournamentDurationLabel));
                return;
            }

            RebuildRecentResults(tournament);
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

            // Состав info card aggregates — recompute on every refresh tick so
            // late registrations / team edits show up without a manual reload.
            OnPropertyChanged(nameof(WrestlersCount));
            OnPropertyChanged(nameof(TeamsCount));
            OnPropertyChanged(nameof(RegionsCount));

            // Network card derives from Tournament.Settings + the live IP probe;
            // refresh on tournament-change ticks so the picker reflects the
            // current network state when the operator returns to this page.
            OnPropertyChanged(nameof(Settings));
            OnPropertyChanged(nameof(SelectedAnnounceAddress));
            OnPropertyChanged(nameof(AnnounceAddressOptions));
            OnPropertyChanged(nameof(IsAnnounceAddressPickerVisible));
        }

        private const int RecentResultsLimit = 10;

        private void RebuildRecentResults(Entities.Tournament tournament)
        {
            var carpetByGroup = new Dictionary<Guid, Carpet>();
            foreach (var carpet in tournament.Carpets)
            {
                foreach (var group in carpet.Groups)
                {
                    carpetByGroup[group.ID] = carpet;
                }
            }

            var top = tournament.Groups
                .Where(g => g.Bracket != null)
                .SelectMany(g => g.Bracket.Rounds.SelectMany(r => r.RoundMatches),
                    (g, m) => new { Group = g, Match = m })
                .Where(x => x.Match.Status == MatchStatusEnum.Completed && x.Match.StartDateTime.HasValue)
                .OrderByDescending(x => x.Match.StartDateTime.Value)
                .Take(RecentResultsLimit)
                .Select(x => BuildSummary(x.Group, x.Match, carpetByGroup))
                .ToList();

            RecentResults.Clear();
            foreach (var summary in top)
            {
                RecentResults.Add(summary);
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
