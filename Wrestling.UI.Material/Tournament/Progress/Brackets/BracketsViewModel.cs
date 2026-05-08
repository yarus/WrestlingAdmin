using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using MaterialDesignThemes.Wpf;
using Wrestling.Entities;
using Wrestling.UI.Material.Match;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Material.Tournament.Print.PrintBracket;
using Wrestling.UI.Material.Tournament.Progress.Schedule;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Tournament.Progress.Brackets
{
    public class BracketsViewModel : TournamentViewModelBase
    {
        #region Fields

        private Carpet _selectedCarpet;
        private ObservableCollection<Carpet> _carpets;
        
        private ICommand _openMatchCommand;
        private ICommand _changeCarpetCommand;
        private ICommand _printBracketCommand;

        private IList<CommandButtonItem> _quickButtons;

        #endregion

        public BracketsViewModel(IDiContainer container) : base(container)
        {
        }
        
        public override string PageTitle => "Турнирная Сетка";

        public override bool IsBackButtonAvailable => true;

        public override void InitData()
        {
            base.InitData();

            if (Tournament == null)
            {
                throw new InvalidOperationException("Tournament is not set on the data context. Navigate to a tournament before opening this view.");
            }

            _quickButtons = null;
            Carpets = DataContext.Tournament.Carpets;

            if (Carpets.Count > 0 && _selectedCarpet == null || (Carpets.Count > 0 && !Carpets.Contains(SelectedCarpet))) SelectedCarpet = Carpets[0];
        }

        public override IList<CommandButtonItem> QuickButtons
        {
            get
            {
                return _quickButtons ??
                       (
                           _quickButtons = new List<CommandButtonItem>
                           {
                               new CommandButtonItem("Открыть расписание схваток", PackIconKind.Receipt, new RelayCommand(param => OpenSchedule(), param => true))
                           }
                       );
            }
        }

        #region Binding Properties
        
        public ObservableCollection<Carpet> Carpets
        {
            get { return _carpets; }
            set
            {
                _carpets = value;

                OnPropertyChanged("Carpets");
            }
        }

        public Carpet SelectedCarpet
        {
            get { return _selectedCarpet; }
            set
            {
                _selectedCarpet = value;
                
                OnPropertyChanged("SelectedCarpet");
            }
        }
        
        #endregion

        #region Command Properties
        
        public ICommand OpenMatchCommand
        {
            get
            {
                if (_openMatchCommand == null)
                {
                    _openMatchCommand = new RelayCommand(param => OpenMatch(param as WrestlingMatch), param => param != null);
                }
                return _openMatchCommand;
            }
        }

        public ICommand PrintBracketCommand
        {
            get
            {
                if (_printBracketCommand == null)
                {
                    _printBracketCommand = new RelayCommand(param => PrintBracket(param as AgeWeightGroup), param => param != null);
                }
                return _printBracketCommand;
            }
        }

        public ICommand ChangeCarpetCommand
        {
            get
            {
                if (_changeCarpetCommand == null)
                {
                    _changeCarpetCommand = new RelayCommand(param => ChangeCarpet(param as Carpet), param => param != null);
                }
                return _changeCarpetCommand;
            }
        }

        #endregion

        #region Private Methods

        private void ChangeCarpet(Carpet carpet)
        {
            SelectedCarpet = carpet;
        }

        private void PrintBracket(AgeWeightGroup group)
        {
            if (group?.Bracket == null) return;

            DataContext.Group = group;

            ShowPrintPreview(new PrintBracketViewModel(DiContainer));
        }

        private void OpenSchedule()
        {
            NavigateToView<ScheduleViewModel>();
        }

        // Brackets is a fullscreen overlay launched from Conducting (and reachable
        // via the toggle from Schedule). Back goes to the admin landing — same
        // reasoning as ScheduleViewModel.OnBackCommand.
        protected override void OnBackCommand()
        {
            NavigateToView<Conducting.ConductingViewModel>();
        }

        private void OpenMatch(WrestlingMatch match)
        {
            if (match == null) return;

            if (match.Status == MatchStatusEnum.Completed)
            {
                DataContext.Group = DataContext.Tournament.Groups.FirstOrDefault(g => g.ID == match.GroupID);
                DataContext.WrestlingMatch = match;
                NavigateToView<MatchResultsViewModel>();
            }
            else if (match.IsMatchCanStart)
            {
                DataContext.Group = DataContext.Tournament.Groups.FirstOrDefault(g => g.ID == match.GroupID);
                DataContext.WrestlingMatch = match;
                NavigateToView<MatchControlViewModel>();
            }
        }

        #endregion
    }
}