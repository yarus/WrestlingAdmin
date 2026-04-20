using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Wrestling.Entities
{
    // A named, independently-rotating set of slides. A tournament has many
    // channels; a slide-host window is bound to one channel, so different
    // monitors can show different content at their own pace.
    public class SlideChannel : INotifyPropertyChanged
    {
        private string _name;
        private ObservableCollection<ScreenSlide> _slides;
        private int _sliderMaxSecond;
        private bool _isOpen;

        public SlideChannel()
        {
            _slides = new ObservableCollection<ScreenSlide>();
        }

        public string Name
        {
            get { return _name; }
            set
            {
                _name = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<ScreenSlide> Slides
        {
            get { return _slides; }
            set
            {
                _slides = value;
                OnPropertyChanged();
            }
        }

        // Fallback rotation duration in seconds when an individual slide has
        // Duration == 0. Channel-scoped so different monitors can cycle at
        // different rates.
        public int SliderMaxSecond
        {
            get { return _sliderMaxSecond; }
            set
            {
                _sliderMaxSecond = value;
                OnPropertyChanged();
            }
        }

        // Transient runtime state. True while a slide-host window is displaying
        // this channel; flipped by SliderWindowManager on open/close. Not
        // serialized (the adapter does not map this field), so it stays session-local.
        public bool IsOpen
        {
            get { return _isOpen; }
            set
            {
                _isOpen = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
