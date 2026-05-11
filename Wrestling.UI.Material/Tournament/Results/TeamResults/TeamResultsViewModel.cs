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
            Items = _resultsService.GetOrderedTeamResults(orderer).ToList();
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
