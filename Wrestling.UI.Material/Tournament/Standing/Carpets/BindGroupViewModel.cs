using System.Collections.Generic;
using System.Linq;
using Wrestling.Entities;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Tournament.Standing.Carpets
{
    public class BindGroupViewModel : ViewModelBase
    {
        private List<AgeWeightGroup> _groups;
        private AgeWeightGroup _selectedGroup;

        public BindGroupViewModel(IDiContainer container) : base(container)
        {
        }

        public override void InitData()
        {
            base.InitData();

            Groups = new List<AgeWeightGroup>(DataContext.Tournament.Groups.Where(g => !g.CarpetID.HasValue && g.IsBracketGenerated));
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