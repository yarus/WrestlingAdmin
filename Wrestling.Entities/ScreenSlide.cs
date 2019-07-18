using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Wrestling.Entities
{
    public class ScreenSlide : INotifyPropertyChanged, ICloneable
    {
        private string _title;
        private string _slideType;
        private int _duration;
        private Dictionary<string, object> _namedValues;

        public ScreenSlide()
        {
            _namedValues = new Dictionary<string, object>();
        }

        public string Title
        {
            get { return _title; }
            set
            {
                _title = value;
                OnPropertyChanged();
            }
        }

        public string SlideType
        {
            get { return _slideType; }
            set
            {
                _slideType = value;
                OnPropertyChanged();
            }
        }

        public int Duration
        {
            get { return _duration; }
            set
            {
                _duration = value;
                OnPropertyChanged();
            }
        }

        public Dictionary<string, object> NamedValues
        {
            get { return _namedValues; }
            set
            {
                _namedValues = value;
                OnPropertyChanged();
            }
        }
        
        public event PropertyChangedEventHandler PropertyChanged;
        
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void Sync(ScreenSlide item)
        {
            Title = item.Title;
            Duration = item.Duration;
            SlideType = item.SlideType;
            NamedValues = item.NamedValues;
        }

        public object Clone()
        {
            var item = new ScreenSlide();
            item.Sync(this);
            return item;
        }

        public object GetNamedValue(string key)
        {
            return NamedValues.ContainsKey(key) ? NamedValues[key] : null;
        }

        public void SetNamedValue(string key, object value)
        {
            if (value == null && NamedValues.ContainsKey(key))
            {
                NamedValues.Remove(key);
            }

            if (NamedValues.ContainsKey(key))
            {
                NamedValues[key] = value;
            }
            else
            {
                NamedValues.Add(key, value);
            }
        }
    }
}
