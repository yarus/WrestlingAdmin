using System;
using System.Collections.Generic;
using System.Linq;
using Wrestling.Entities;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Match
{
    public class SetWinTypeViewModel : ViewModelBase
    {
        private MatchWinTypeEnum? _item;

        private IList<MatchWinTypeEnum> _winTypes;

        public SetWinTypeViewModel(IDiContainer container, MatchWinTypeEnum? value, IList<MatchWinTypeEnum> availableWinTypes) : base(container)
        {
            WinTypes = Enum.GetValues(typeof(MatchWinTypeEnum)).Cast<MatchWinTypeEnum>()
                .Where(o => availableWinTypes.Contains(o))
                .ToList();

            SelectedItem = value;
        }

        public MatchWinTypeEnum? SelectedItem
        {
            get { return _item; }
            set
            {
                _item = value;                

                OnPropertyChanged("SelectedItem");
            }
        }

        public IList<MatchWinTypeEnum> WinTypes
        {
            get { return _winTypes; }
            set
            {
                _winTypes = value;

                OnPropertyChanged("WinTypes");
            }
        }
    }
}
