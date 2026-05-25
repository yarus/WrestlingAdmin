using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Wrestling.Entities
{
    // A tournament can be split into operational parts that share the same
    // mats but run sequentially with their own award ceremonies. Examples
    // include "morning younger groups" → award break → "afternoon older
    // groups". Groups, mats, and per-(part, mat) match numbering all key off
    // these parts; team rankings still aggregate across every part of the
    // tournament. Order is the position in Tournament.Parts collection;
    // there is no separate Order field — reordering is intentionally not
    // supported, the operator deletes-and-recreates if a mistake was made.
    public class TournamentPart : INotifyPropertyChanged
    {
        private Guid _id;
        private string _name;

        public Guid ID
        {
            get { return _id; }
            set
            {
                _id = value;
                OnPropertyChanged();
            }
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

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
