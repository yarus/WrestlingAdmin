using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using MaterialDesignThemes.Wpf;
using Wrestling.Entities;
using Wrestling.Entities.Bracket;
using Wrestling.Entities.Bracket.Seeding;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Material.Tournament.Print.PrintApplications;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Tournament.Standing.Draw
{
    public class DrawViewModel : TournamentViewModelBase, IStandingPageViewModel
    {
        #region Fields

        private IMatchNumbersGenerator _matchNumbersGenerator;
        private ISeedingStrategy _seedingStrategy;

        private ICommand _generateBracketCommand;
        private ICommand _removeBracketCommand;
        private ICommand _printProtocolCommand;
        private ICommand _regenerateAllBrackets;

        private List<IGroupBracketProcessor> _drawTypes;
        private ObservableCollection<AgeWeightGroup> _groups;

        private bool IsTeamTournament => true;

        #endregion

        public DrawViewModel(IDiContainer container) : base(container)
        {
        }

        public override void InitData()
        {
            base.InitData();

            if (DataContext.Tournament == null)
            {
                throw new ApplicationException("Tournament property is not set!");
            }

            _matchNumbersGenerator = Resolve<IMatchNumbersGenerator>();

            _drawTypes = Resolve<List<IGroupBracketProcessor>>();
            _seedingStrategy = Resolve<ISeedingStrategy>();

            var groups = DataContext.Tournament.Groups.OrderBy(g => g.IsFemale).ThenByDescending(g => g.BirthYearMin).ThenBy(g => g.WeightMax).ToList();
            foreach (var group in groups)
            {
                SeedWrestlers(group);
            }

            Groups = new ObservableCollection<AgeWeightGroup>(groups);

            // Check groups
            foreach (var wrestler in DataContext.Tournament.Wrestlers)
            {
                var group = Groups.FirstOrDefault(gr => gr.ID == wrestler.GroupID);
                if (group != null)
                {                    
                    if (group.Wrestlers.FirstOrDefault(wr => wr.ID == wrestler.ID) == null)
                    {
                        group.Wrestlers.Add(wrestler);
                    }
                }
                else
                {
                    wrestler.GroupID = null;
                    wrestler.GroupName = string.Empty;
                }
            }
        }

        #region Binding Properties

        public int GroupsCount => DataContext.Tournament.GroupsCount;
        public int WrestlersCount => Groups?.SelectMany(gr => gr.Wrestlers).Count() ?? 0;
        public int MatchesCount => DataContext.Tournament.MatchesCount;

        public string PageName => "Жеребьевка";
        public override string PageTitle => "Жеребьевка Участников";
        
        public ObservableCollection<AgeWeightGroup> Groups
        {
            get => _groups;
            set
            {
                _groups = value;

                OnPropertyChanged("Groups");
            }
        }

        #endregion

        #region Command Properties

        public ICommand RegenerateAllBrackets
        {
            get
            {
                if (_regenerateAllBrackets == null)
                {
                    _regenerateAllBrackets = new RelayCommand(param => RegenerateBrackets(), param => param != null);
                }
                return _regenerateAllBrackets;
            }
        }
        
        public ICommand PrintProtocolCommand
        {
            get
            {
                if (_printProtocolCommand == null)
                {
                    _printProtocolCommand = new RelayCommand(param => PrintProtocol(param as AgeWeightGroup), param => param != null);
                }
                return _printProtocolCommand;
            }
        }

        public ICommand GenerateBracketCommand
        {
            get
            {
                if (_generateBracketCommand == null)
                {
                    _generateBracketCommand = new RelayCommand(param => GenerateBracket(param as AgeWeightGroup), param => param != null);
                }
                return _generateBracketCommand;
            }
        }

        public ICommand RemoveBracketCommand
        {
            get
            {
                if (_removeBracketCommand == null)
                {
                    _removeBracketCommand = new RelayCommand(param => RemoveBracket(param as AgeWeightGroup), param => param != null);
                }
                return _removeBracketCommand;
            }
        }

        #endregion

        #region Private Methods

        private void RegenerateBrackets()
        {
            if (Dialog.ShowMessageBox(this, "Вы уверены, что хотите перегенерировать все сетки! Это приведет к потере текущих результатов турнира!", "Требуется подтверждение", MessageBoxButton.OKCancel, MessageBoxImage.Information) != MessageBoxResult.OK) return;

            foreach (var ageWeightGroup in Groups)
            {
                SeedWrestlers(ageWeightGroup);

                var drawType = GetDrawTypeForGroup(ageWeightGroup);

                if (drawType == null)
                {
                    continue;
                }
                
                drawType.Generate(DataContext.Tournament, ageWeightGroup);
                
                foreach (var wr in ageWeightGroup.Wrestlers)
                {
                    wr.FinalPlace = null;
                    wr.IsSeedFixed = true;
                }
                
                if (ageWeightGroup.Bracket != null)
                {
                    if (DataContext.Tournament.Carpets.FirstOrDefault(c => c.Groups.Contains(ageWeightGroup)) != null)
                    {
                        _matchNumbersGenerator.Generate(DataContext.Tournament, _drawTypes);
                    }

                    // We need to refresh Rounds collection to redraw it on UI
                    ageWeightGroup.Bracket.Rounds = new List<GroupRound>(ageWeightGroup.Bracket.Rounds);

                    OnPropertyChanged("MatchesCount");
                }
            }
        }

        private IGroupBracketProcessor GetDrawTypeForGroup(AgeWeightGroup group)
        {
            IGroupBracketProcessor drawType;
                
            if (group.Wrestlers.Count <= 5)
            {
                drawType = _drawTypes.First(x => x.Code == BracketTypeEnum.RoundRobin.ToString());
            } 
            else if (group.Wrestlers.Count > 5 && group.Wrestlers.Count < 8)
            {
                drawType = _drawTypes.First(x => x.Code == BracketTypeEnum.SubGroupsIntoOlympic.ToString());
            }
            else
            {
                drawType = _drawTypes.First(x => x.Code == BracketTypeEnum.OlympicConsilationFinalists.ToString());
            }

            return drawType;
        }

        private void PrintProtocol(AgeWeightGroup group)
        {
            if (group?.Bracket == null) return;

            DataContext.Group = group;

            ShowPrintPreview(new PrintApplicationsViewModel(DiContainer));
        }

        private void RemoveBracket(AgeWeightGroup group)
        {
            if (Dialog.ShowMessageBox(this, "Вы уверены, что хотите удалить результаты жеребьевки?", "Требуется подтверждение", MessageBoxButton.OKCancel, MessageBoxImage.Information) != MessageBoxResult.OK) return;

            group.Bracket = null;

            OnPropertyChanged("MatchesCount");
        }

        private async void GenerateBracket(AgeWeightGroup group)
        {
            if (group == null) return;
            
            var vm = new AddBracketViewModel(DiContainer, group);
            vm.InitData();

            var view = new AddBracketDialog
            {
                DataContext = vm
            };

            var result = await DialogHost.Show(view, "RootDialog");

            if (result != null && (bool)result)
            {
                SeedWrestlers(group);

                var drawType = _drawTypes.FirstOrDefault(d => d.Title == vm.SelectedDrawType.Title);

                if (drawType == null) throw new ApplicationException("Wrong Bracket type!");

                drawType.Generate(DataContext.Tournament, group);

                foreach (var wr in group.Wrestlers)
                {
                    wr.FinalPlace = null;
                    wr.IsSeedFixed = true;
                }

                if (group.Bracket != null)
                {
                    if (DataContext.Tournament.Carpets.FirstOrDefault(c => c.Groups.Contains(group)) != null)
                    {
                        _matchNumbersGenerator.Generate(DataContext.Tournament, _drawTypes);
                    }

                    // We need to refresh Rounds collection to redraw it on UI
                    group.Bracket.Rounds = new List<GroupRound>(group.Bracket.Rounds);

                    OnPropertyChanged("MatchesCount");
                }
            }
        }
        
        // Delegates to the injected ISeedingStrategy (see App.xaml.cs). The
        // strategy is responsible for honoring IsSeedFixed locks, rewriting
        // SeedNumber to a contiguous 1..N range, and sorting group.Wrestlers
        // by the new SeedNumber.
        private void SeedWrestlers(AgeWeightGroup group)
        {
            // InitData is the first call site — _seedingStrategy may not be
            // resolved yet when unit tests bypass InitData. Guard defensively.
            if (_seedingStrategy == null)
            {
                _seedingStrategy = Resolve<ISeedingStrategy>();
            }
            _seedingStrategy.Seed(group);
        }

        #endregion
    }
}