using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Wrestling.Entities.Results;
using Wrestling.Providers;
using Wrestling.UI.Material.Tournament.Standing;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Tournament.Results.TeamResults
{
    public class TeamResultsViewModel : TournamentViewModelBase, IStandingPageViewModel
    {
        public string PageName => "Командные";


        private IResultsService _resultsService;
        private bool _resultsSubscribed;

        private readonly IList<TeamResultsSystemItem> _systems = new List<TeamResultsSystemItem>
        {
            new TeamResultsSystemItem("OlympicOrderer", "Олимпийская"),
            new TeamResultsSystemItem("MedalsOrderer", "По количеству медалей"),
            new TeamResultsSystemItem("PointsOrderer", "Rosbos")
        };

        private TeamResultsSystemItem _selectedSystem;
        private IList<TournamentTeamResult> _items;
        private ICommand _changeSystemCommand;

        public TeamResultsViewModel(IDiContainer container) : base(container)
        {
            _selectedSystem = _systems[0];
        }

        public override bool IsBackButtonAvailable => true;

        public override string PageTitle => "Командные итоги";

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

            Reorder();
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
            if (orderer == null)
            {
                Items = _resultsService.TeamResults.ToList();
                return;
            }

            var ordered = orderer.GetOrderedResults(_resultsService.TeamResults.ToList());
            Items = ordered ?? new List<TournamentTeamResult>();
        }
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
