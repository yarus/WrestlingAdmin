using Wrestling.Entities;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Tournament.Standing.Registration
{
    public class SetWeightViewModel : ViewModelBase
    {
        private AgeWeightGroup _group;
        private Wrestler _wrestler;

        public SetWeightViewModel(IDiContainer container, AgeWeightGroup group, Wrestler wrestler) : base(container)
        {
            _group = group;
            _wrestler = wrestler;
        }
        
        public AgeWeightGroup Group
        {
            get { return _group; }
            set
            {
                _group = value;
                OnPropertyChanged("Group");
            }
        }

        public Wrestler Wrestler
        {
            get { return _wrestler; }
            set
            {
                _wrestler = value;
                OnPropertyChanged("Wrestler");
            }
        }
    }
}