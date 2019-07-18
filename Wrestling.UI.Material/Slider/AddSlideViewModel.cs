using System.Collections.Generic;
using System.Linq;
using Wrestling.Entities;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Material.Slider.Slides;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Slider
{
    public class AddSlideViewModel : ViewModelBase
    {
        private List<ISlideType> _slideTypes;
        private ISlideType _selectedSlideType;
        private ScreenSlide _item;

        public AddSlideViewModel(IDiContainer container) : base(container)
        {
        }

        public override void InitData()
        {
            base.InitData();

            _slideTypes = Resolve<List<ISlideType>>();

            if (_item != null && !string.IsNullOrEmpty(_item.SlideType))
            {
                var type = _slideTypes.FirstOrDefault(p => p.SlideType == _item.SlideType);
                if (type != null)
                {
                    SelectedSlideType = type;
                }
            }
        }

        public ScreenSlide Item
        {
            get { return _item; }
            set
            {
                _item = value;
                OnPropertyChanged("Item");
            }
        }

        public ISlideType SelectedSlideType
        {
            get { return _selectedSlideType; }
            set
            {
                _selectedSlideType = value;

                if (_selectedSlideType != null)
                {
                    Item.SlideType = _selectedSlideType.SlideType;

                    _selectedSlideType.SettingsControl?.InitContext(Item);
                }
                else
                {
                    Item.SlideType = string.Empty;
                }

                OnPropertyChanged("SelectedSlideType");
            }
        }

        public List<ISlideType> SlideTypes
        {
            get { return _slideTypes; }
            set
            {
                _slideTypes = value;
                OnPropertyChanged("SlideTypes");
            }
        }
    }
}