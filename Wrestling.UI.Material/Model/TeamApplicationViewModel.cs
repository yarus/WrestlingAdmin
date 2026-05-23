using System;
using System.Collections.ObjectModel;
using System.Linq;
using Wrestling.Entities;

namespace Wrestling.UI.Material.Model
{
    public class TeamApplicationViewModel : ObservableObject
    {
        private TeamApplication _teamApplication;
        private Entities.Tournament _tournament;
        private string _filter;
        private bool _isOnlyUnapprovedVisible;

        public TeamApplicationViewModel(TeamApplication teamApplication, Entities.Tournament tournament)
        {
            _teamApplication = teamApplication;
            _tournament = tournament;
        }

        public TeamApplication Team => _teamApplication;

        public void SetFilter(string filter, bool isOnlyUnapprovedVisible)
        {
            _filter = filter;
            _isOnlyUnapprovedVisible = isOnlyUnapprovedVisible;

            OnPropertyChanged("Wrestlers");
        }

        public ObservableCollection<Wrestler> Wrestlers
        {
            get
            {
                var teamWrestlers = _tournament.Wrestlers.Where(x => x.TeamID == _teamApplication.ID);

                if (string.IsNullOrEmpty(_filter) && !_isOnlyUnapprovedVisible)
                {
                    return new ObservableCollection<Wrestler>(teamWrestlers.OrderBy(x => x.LastFirstName));
                }

                var hasTextFilter = !string.IsNullOrEmpty(_filter) && _filter.Length > 2;
                var teamNameMatched = hasTextFilter
                    && (ContainsCi(_teamApplication.ShortName, _filter)
                        || ContainsCi(_teamApplication.FullName, _filter)
                        || ContainsCi(_teamApplication.City, _filter));

                var result = teamWrestlers.Where(w =>
                    (!_isOnlyUnapprovedVisible || !w.IsRegistrationApproved)
                    && (!hasTextFilter
                        || teamNameMatched
                        || (!string.IsNullOrEmpty(w.FullName)
                            && w.FullName.IndexOf(_filter, StringComparison.InvariantCultureIgnoreCase) >= 0)));

                return new ObservableCollection<Wrestler>(result.OrderBy(x => x.LastFirstName));
            }
        }

        public bool IsApplicationValid => Wrestlers.FirstOrDefault(w => !w.IsApplicationValid) == null;

        private static bool ContainsCi(string source, string value) =>
            !string.IsNullOrEmpty(source)
            && !string.IsNullOrEmpty(value)
            && source.IndexOf(value, StringComparison.InvariantCultureIgnoreCase) >= 0;
    }
}
