using Wrestling.UI.Material.Model;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Tournament.Standing.Details
{
    public partial class GenerateGroupsDialogViewModel : ViewModelBase
    {
        private string _generateText;
        
        public string GenerateText
        {
            get
            {
                return _generateText;
            }
            set
            {
                if (_generateText != value)
                {
                    _generateText = value;
                    OnPropertyChanged("GenerateText");
                }
            }
        }
        
        public GenerateGroupsDialogViewModel(IDiContainer container) : base(container)
        {
        }
    }   
}