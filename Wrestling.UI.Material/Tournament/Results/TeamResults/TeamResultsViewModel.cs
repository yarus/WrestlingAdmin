using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Wrestling.Entities.Results;
using Wrestling.Providers;
using Wrestling.UI.Material.Tournament.Standing;
using Wrestling.UI.Utils;
using Wrestling.UI.Utils.Localization;

namespace Wrestling.UI.Material.Tournament.Results.TeamResults
{
    public class TeamResultsViewModel : TournamentViewModelBase, IStandingPageViewModel
    {
        // T inherited from TournamentViewModelBase.
        public string PageName => T("Team_PageName", "Командные");


        private IResultsService _resultsService;
        private bool _resultsSubscribed;

        // System Name strings are user-facing labels — the buttons render via
        // {Binding Name}. Kept as plain literals at construction; if the user
        // wants live language switching the easiest move is to look up via
        // T() inside the TeamResultsSystemItem.Name getter.
        private readonly IList<TeamResultsSystemItem> _systems = new List<TeamResultsSystemItem>
        {
            new TeamResultsSystemItem("OlympicOrderer", T("TeamSystem_Olympic", "Олимпийская")),
            new TeamResultsSystemItem("MedalsOrderer", T("TeamSystem_Medals", "По количеству медалей")),
            new TeamResultsSystemItem("PointsOrderer", T("TeamSystem_Points", "По квалификационным баллам"))
        };

        private TeamResultsSystemItem _selectedSystem;
        private IList<TournamentTeamResult> _items;
        private IList<TeamResultsPartItem> _partFilters;
        private TeamResultsPartItem _selectedPartFilter;
        private ICommand _changeSystemCommand;

        public TeamResultsViewModel(IDiContainer container) : base(container)
        {
            _selectedSystem = _systems[0];
        }

        public override bool IsBackButtonAvailable => true;

        public override string PageTitle => T("Team_PageTitle", "Командные итоги");

        public IList<TeamResultsSystemItem> Systems => _systems;

        public TeamResultsSystemItem SelectedSystem
        {
            get => _selectedSystem;
            set
            {
                if (ReferenceEquals(_selectedSystem, value)) return;
                _selectedSystem = value;
                OnPropertyChanged(nameof(SelectedSystem));
                Reorder();
            }
        }

        public IList<TournamentTeamResult> Items
        {
            get => _items;
            private set
            {
                _items = value;
                OnPropertyChanged(nameof(Items));
            }
        }

        // Part filter tabs: "Сумма всех частей" (PartId == null) followed by one
        // item per tournament part. The selector is hidden on single-part
        // tournaments — the sum then equals the lone part anyway.
        public IList<TeamResultsPartItem> PartFilters
        {
            get => _partFilters;
            private set
            {
                _partFilters = value;
                OnPropertyChanged(nameof(PartFilters));
            }
        }

        public bool HasMultipleParts => (DataContext?.Tournament?.Parts?.Count ?? 0) > 1;

        public TeamResultsPartItem SelectedPartFilter
        {
            get => _selectedPartFilter;
            set
            {
                if (ReferenceEquals(_selectedPartFilter, value)) return;
                _selectedPartFilter = value;
                OnPropertyChanged(nameof(SelectedPartFilter));
                Reorder();
            }
        }

        public ICommand ChangeSystemCommand => _changeSystemCommand ??
            (_changeSystemCommand = new RelayCommand(param =>
            {
                if (param is TeamResultsSystemItem item)
                {
                    SelectedSystem = item;
                }
            }, _ => true));

        public override void InitData()
        {
            base.InitData();

            if (_resultsService == null)
            {
                _resultsService = Resolve<IResultsService>();
            }

            if (_resultsService != null && !_resultsSubscribed)
            {
                _resultsService.ResultsChanged += OnResultsChanged;
                _resultsSubscribed = true;
            }

            BuildPartFilters();
            Reorder();
        }

        // Rebuilt on every page entry: parts may have been added / renamed /
        // removed since last visit. Resets the selection to the sum (index 0).
        private void BuildPartFilters()
        {
            var list = new List<TeamResultsPartItem>
            {
                new TeamResultsPartItem(null, T("TeamResults_AllParts", "Сумма всех частей"))
            };

            var parts = DataContext?.Tournament?.Parts;
            if (parts != null)
            {
                foreach (var part in parts)
                {
                    list.Add(new TeamResultsPartItem(part.ID, part.Name));
                }
            }

            PartFilters = list;
            _selectedPartFilter = list[0];
            OnPropertyChanged(nameof(SelectedPartFilter));
            OnPropertyChanged(nameof(HasMultipleParts));
        }

        protected override void OnBackCommand()
        {
            // Phase 6 wrapper handles navigation; sub-page back is no-op.
        }

        private void OnResultsChanged()
        {
            Reorder();
        }

        private void Reorder()
        {
            if (_resultsService == null || _selectedSystem == null)
            {
                Items = new List<TournamentTeamResult>();
                return;
            }

            var orderer = Resolve<ITeamResultsOrderer>(_selectedSystem.OrdererKey);
            Items = _resultsService.GetOrderedTeamResults(orderer, _selectedPartFilter?.PartId).ToList();
        }
    }

    // One entry in the part-filter strip on «Командные итоги».
    // PartId == null is the "sum of all parts" tab.
    public class TeamResultsPartItem
    {
        public TeamResultsPartItem(Guid? partId, string name)
        {
            PartId = partId;
            Name = name;
        }

        public Guid? PartId { get; }
        public string Name { get; }
    }

    public class TeamResultsSystemItem
    {
        public TeamResultsSystemItem(string ordererKey, string name)
        {
            OrdererKey = ordererKey;
            Name = name;
        }

        public string OrdererKey { get; }
        public string Name { get; }
    }
}
