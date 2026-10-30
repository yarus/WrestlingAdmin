using System.Collections.Generic;
using System.Linq;
using Wrestling.Entities;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Tournament.Standing.Mats
{
    public class BindGroupViewModel : ViewModelBase
    {
        private List<AgeWeightGroup> _groups;
        private AgeWeightGroup _selectedGroup;
        private System.Nullable<System.Guid> _partFilter;
        private IMatRedistributionService _redistribution;

        public BindGroupViewModel(IDiContainer container) : base(container)
        {
        }

        // Target part — unassigned groups from other parts are also offered
        // when their match state allows moving them into this part.
        // Set by the (Part, Mat) bind flow before InitData.
        public System.Nullable<System.Guid> PartFilter
        {
            get => _partFilter;
            set => _partFilter = value;
        }

        public override void InitData()
        {
            base.InitData();
            _redistribution = Resolve<IMatRedistributionService>();
            SelectedGroup = null;

            Groups = new List<AgeWeightGroup>(DataContext.Tournament.Groups
                .Where(g => !g.MatID.HasValue
                            && g.IsBracketGenerated
                            && !_redistribution.HasLiveMatch(g)
                            && (!_partFilter.HasValue || _redistribution.CanMoveGroupToPart(g, _partFilter.Value))))
                .OrderByDescending(g => g.BirthYearMin).ThenBy(g => g.WeightMax).ToList();
        }

        public AgeWeightGroup SelectedGroup
        {
            get { return _selectedGroup; }
            set
            {
                _selectedGroup = value;
                OnPropertyChanged("SelectedGroup");
            }
        }

        public List<AgeWeightGroup> Groups
        {
            get { return _groups; }
            set
            {
                _groups = value;
                OnPropertyChanged("Groups");
            }
        }
    }
}
