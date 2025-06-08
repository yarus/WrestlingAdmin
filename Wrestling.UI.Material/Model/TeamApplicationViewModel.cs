using System.Collections.ObjectModel;
using System.Globalization;
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
                if (string.IsNullOrEmpty(_filter) && !_isOnlyUnapprovedVisible)
                {
                    return new ObservableCollection<Wrestler>(_tournament.Wrestlers.Where(x => x.TeamID == _teamApplication.ID).OrderBy(x => x.LastFirstName));
                }

                var result = _tournament.Wrestlers.Where(w => (!_isOnlyUnapprovedVisible || !w.IsRegistrationApproved)
                   && (_filter == null || _filter.Length <= 2 || (_filter.Length > 2 && w.LastName.StartsWith(_filter, true, CultureInfo.InvariantCulture)))).ToList();

                return new ObservableCollection<Wrestler>(result.OrderBy(x => x.LastFirstName));
            }
        }

        public bool IsApplicationValid => Wrestlers.FirstOrDefault(w => !w.IsApplicationValid) == null;
    }
}
