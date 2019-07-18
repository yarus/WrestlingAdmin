using Wrestling.Entities;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Tournament.Standing.Details
{
    public class AddGroupViewModel : ViewModelBase
    {
        private AgeWeightGroup _item;

        public AddGroupViewModel(IDiContainer container, AgeWeightGroup item) : base(container)
        {
            _item = item;
        }

        public bool? IsFemaleF
        {
            get
            {
                return _item != null ? !_item.IsFemale : false;
            }
            set
            {
                if (_item != null && value.HasValue)
                {
                    _item.IsFemale = false;
                    OnPropertyChanged("IsFemaleT");
                }
            }
        }

        public bool? IsFemaleT
        {
            get
            {
                return _item != null ? _item.IsFemale : false;
            }
            set
            {
                if (_item != null && value.HasValue)
                {
                    _item.IsFemale = true;
                    OnPropertyChanged("IsFemaleF");
                }
            }
        }


        public AgeWeightGroup Item
        {
            get { return _item; }
            set
            {
                _item = value;
                OnPropertyChanged("Item");
            }
        }
    }
}