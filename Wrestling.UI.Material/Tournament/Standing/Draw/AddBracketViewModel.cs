using System.Collections.Generic;
using System.Linq;
using Wrestling.Entities;
using Wrestling.Entities.Bracket;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Tournament.Standing.Draw
{
    public class AddBracketViewModel : ViewModelBase
    {
        private List<IGroupBracketProcessor> _drawTypes;
        private IGroupBracketProcessor _selectedDrawType;
        private AgeWeightGroup _group;

        public AddBracketViewModel(IDiContainer container, AgeWeightGroup item) : base(container)
        {
            _group = item;
        }

        public override void InitData()
        {
            base.InitData();

            _drawTypes = GetFilteredBracketProcessors(_group);
            
            if (_group.Bracket != null)
            {
                var type = _drawTypes.FirstOrDefault(d => d.Code == _group.Bracket.BracketTypeCode);

                if (type != null)
                {
                    SelectedDrawType = type;
                }
            }
        }

        private List<IGroupBracketProcessor> GetFilteredBracketProcessors(AgeWeightGroup group)
        {
            var result = new List<IGroupBracketProcessor>();

            var allProcessors = Resolve<List<IGroupBracketProcessor>>();

            foreach (var groupBracketProcessor in allProcessors)
            {
                if ((!groupBracketProcessor.AthletesMinCount.HasValue ||
                     groupBracketProcessor.AthletesMinCount.Value <= group.Wrestlers.Count) &&
                    (!groupBracketProcessor.AthletesMaxCount.HasValue ||
                     groupBracketProcessor.AthletesMaxCount >= group.Wrestlers.Count))
                {
                    result.Add(groupBracketProcessor);
                }
            }

            return result;
        }

        public IGroupBracketProcessor SelectedDrawType
        {
            get { return _selectedDrawType; }
            set
            {
                _selectedDrawType = value;
                OnPropertyChanged("SelectedDrawType");
            }
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

        public List<IGroupBracketProcessor> DrawTypes
        {
            get { return _drawTypes; }
            set
            {
                _drawTypes = value;
                OnPropertyChanged("DrawTypes");
            }
        }
    }
}