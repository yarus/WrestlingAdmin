using Wrestling.Entities;

namespace Wrestling.UI.Material.Slider
{
    public class ChannelEditorViewModel : ObservableObject
    {
        private string _name;
        private int _sliderMaxSecond;

        public string Name
        {
            get { return _name; }
            set
            {
                _name = value;
                OnPropertyChanged("Name");
            }
        }

        public int SliderMaxSecond
        {
            get { return _sliderMaxSecond; }
            set
            {
                _sliderMaxSecond = value;
                OnPropertyChanged("SliderMaxSecond");
            }
        }
    }
}
