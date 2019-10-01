using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using Wrestling.Entities;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.ScoreScreen
{
    public class ScoreScreenViewModel : ViewModelBase
    {
        private string _tournamentTitle;
        private string _carpetLabel;
        private string _roundName;
        private string _groupLabel;
        private string _wrestler1;
        private string _wrestler2;
        private string _wrestler1TeamName;
        private string _wrestler2TeamName;
        private string _wrestler1TeamEmblem;
        private string _wrestler2TeamEmblem;
        private int _points1;
        private int _points2;
        private int _maxRoundSecond;
        private int _maxTimeoutSecond;
        private int _maxActionSecond;
        private bool _isTimerBackward;
        private bool _isSoundEnabled;
        private bool _isTimeout;
        private int _round;
        private int _wrestler1WarningsNumber;
        private int _wrestler2WarningsNumber;
        private bool _isAction1TimerEnabled;
        private bool _isAction2TimerEnabled;
        private int _mainSeconds;
        private int _secondarySeconds;
        private string _matchFullNumber;
        private int _bestActionRed;
        private int _bestActionBlue;
        private bool _isLastActionRed;
        private bool _isPlayer1WithAdvantage;
        private bool _isPlayer2WithAdvantage;

        private bool _isMainScreenVisible;
        private bool _isWinnerDialogVisible;
        private bool _isUpcomingMatchesVisible;
        private string _winnerTeamEmblem;
        private Wrestler _winner;
        private SolidColorBrush _winnerColorBrush;

        private Carpet _lastMatchCarpet;

        private ObservableCollection<WrestlingMatch> _upcomingMatches;

        public ScoreScreenViewModel(IDiContainer container) : base(container)
        {
            IsMainScreenVisible = true;
        }

        private string _backgroundPath;

        public string BackgroundPath
        {
            get { return _backgroundPath; }
            set
            {
                _backgroundPath = value;
                OnPropertyChanged("BackgroundPath");
            }
        }

        public System.Drawing.Bitmap LogoImage { get; set; }
        public System.Drawing.RectangleF LogoRectangle { get; set; }
        public LogoPositionEnum LogoPosition { get; set; }

        private double _backgroundOpacity;

        public double BackgroundOpacity
        {
            get { return _backgroundOpacity; }
            set
            {
                _backgroundOpacity = value;
                OnPropertyChanged("BackgroundOpacity");
            }
        }

        public void ShowWinner(WrestlingMatch match)
        {
            if (match.Status != MatchStatusEnum.Completed || !match.IsRedWon.HasValue) return;

            /*
            //var vm = new ShowWinnerViewModel(DiContainer, winner);
            //vm.InitData();

            var view = new ShowWinnerDialog
            {
                DataContext = vm
            };
            */

            Winner = match.IsRedWon.Value ? match.WrestlerInRed : match.WrestlerInBlue;

            if (Winner?.TeamID != null)
            {
                var team = DataContext.Tournament.TeamApplications.FirstOrDefault(a => a.ID == Winner.TeamID.Value);
                if (team != null)
                {
                    WinnerTeamEmblem = team.EmblemPath;
                }
            }
            else
            {
                var imgPath = $"{AppDomain.CurrentDomain.BaseDirectory}Images\\";

                WinnerTeamEmblem = $"{imgPath}DefaultLogo.png";
            }

            WinnerColorBrush = new SolidColorBrush(match.IsRedWon.Value ? Colors.Red : Colors.Blue);

            IsMainScreenVisible = false;
            IsWinnerDialogVisible = true;

            Task.Delay(TimeSpan.FromSeconds(5)).ContinueWith((t, _) =>
            {
                WinnerColorBrush = null;
                WinnerTeamEmblem = string.Empty;
                IsWinnerDialogVisible = false;

                if (LastMatchCarpet != null && DataContext.Tournament != null)
                {
                    if (!IsMainScreenVisible)
                    {
                        UpcomingMatches = new ObservableCollection<WrestlingMatch>(LastMatchCarpet.Groups
                            .SelectMany(g => g.Bracket.Rounds)
                            .SelectMany(r => r.RoundMatches).Where(m => m.Status == MatchStatusEnum.Pending)
                            .OrderBy(m => m.MatchNumber).Take(2));

                        if (UpcomingMatches.Count > 0)
                        {
                            IsUpcomingMatchesVisible = true;
                        }
                        else
                        {
                            IsMainScreenVisible = true;
                        }
                    }
                }
                else
                {
                    IsMainScreenVisible = true;
                }

                Winner = null;

            }, null, TaskScheduler.FromCurrentSynchronizationContext());
        }

        public override void InitData()
        {
            base.InitData();
            
            BackgroundPath = GlobalSettings.SliderBackgroundImagePath;
            BackgroundOpacity = GlobalSettings.SliderOpacity;

            IsWinnerDialogVisible = false;
            IsUpcomingMatchesVisible = false;
            IsMainScreenVisible = true;

            if (DataContext.Tournament != null && DataContext.Tournament.Carpets.Count > 0)
            {
                LastMatchCarpet = DataContext.Tournament.Carpets.FirstOrDefault(c => c.ID == DataContext.Group.CarpetID);
            }

            if (DataContext.WrestlingMatch.LastSecondInMatch == 0)
            {
                Round = 1;
                MainSeconds = 0;
                SecondarySeconds = 0;
            }
            else
            {
                if (DataContext.WrestlingMatch.LastSecondInMatch > _maxRoundSecond)
                {
                    Round = 2;
                    MainSeconds = DataContext.WrestlingMatch.LastSecondInMatch - _maxRoundSecond;
                }
                else
                {
                    Round = 1;
                    MainSeconds = DataContext.WrestlingMatch.LastSecondInMatch;
                }
            }

            IsPlayer1WithAdvantage = false;
            IsPlayer2WithAdvantage = false;

            Points1 = DataContext.WrestlingMatch.PointsRed;
            Points2 = DataContext.WrestlingMatch.PointsBlue;

            CarpetLabel = DataContext.Group != null ? DataContext.Group.CarpetLabel : string.Empty;
            IsSoundEnabled = GlobalSettings.IsSoundEnabled;
            IsTimerBackward = GlobalSettings.IsTimerBackward;

            GroupLabel = DataContext.WrestlingMatch.GroupName;

            MaxActionSecond = DataContext.WrestlingMatch.MaxActionSecond;
            MaxRoundSecond = DataContext.WrestlingMatch.MaxRoundSecond;
            MaxTimeoutSecond = DataContext.WrestlingMatch.MaxTimeoutSecond;

            RoundName = DataContext.WrestlingMatch.RoundName;
            Wrestler1TeamName = DataContext.WrestlingMatch.WrestlerInRed.TeamName;
            Wrestler2TeamName = DataContext.WrestlingMatch.WrestlerInBlue.TeamName;
            MatchFullNumber = DataContext.WrestlingMatch.MatchNumber.ToString();

            Wrestler1TeamEmblem = string.Empty;
            Wrestler2TeamEmblem = string.Empty;

            if (DataContext.WrestlingMatch.WrestlerInRed.TeamID.HasValue && DataContext.Tournament != null)
            {
                Wrestler1TeamEmblem = GetTeamEmblem(DataContext.WrestlingMatch.WrestlerInRed.TeamID.Value, DataContext.Tournament.TeamApplications);
            }

            if (DataContext.WrestlingMatch.WrestlerInBlue.TeamID.HasValue && DataContext.Tournament != null)
            {
                Wrestler2TeamEmblem = GetTeamEmblem(DataContext.WrestlingMatch.WrestlerInBlue.TeamID.Value, DataContext.Tournament.TeamApplications);
            }

            Wrestler1WarningsNumber = DataContext.WrestlingMatch.WarningsNumberRed;
            Wrestler2WarningsNumber = DataContext.WrestlingMatch.WarningsNumberBlue;
            BestActionRed = DataContext.WrestlingMatch.BestActionRed;
            BestActionBlue = DataContext.WrestlingMatch.BestActionBlue;
            IsLastActionRed = DataContext.WrestlingMatch.IsLastActionRed;

            if (DataContext.Tournament != null)
            {
                TournamentTitle = DataContext.Tournament.Name;
            }

            Wrestler1 = DataContext.WrestlingMatch.WrestlerInRed.LastFirstNameShort;
            //Wrestler1 = DataContext.WrestlingMatch.WrestlerInRed.LastFirstName;
            Wrestler2 = DataContext.WrestlingMatch.WrestlerInBlue.LastFirstNameShort;
            //Wrestler2 = DataContext.WrestlingMatch.WrestlerInBlue.LastFirstName;
        }

        public void Reset()
        {
            MainSeconds = 0;
            SecondarySeconds = 0;
            Points1 = 0;
            Points2 = 0;
            Wrestler1WarningsNumber = 0;
            Wrestler2WarningsNumber = 0;
            BestActionBlue = 0;
            BestActionRed = 0;
            Round = 1;
            IsAction1TimerEnabled = false;
            IsAction2TimerEnabled = false;
            IsPlayer1WithAdvantage = false;
            IsPlayer2WithAdvantage = false;
            IsTimeout = false;
        }

        private string GetTeamEmblem(Guid teamID, IEnumerable<TeamApplication> apps)
        {
            var team = apps.FirstOrDefault(t => t.ID == teamID);

            return team != null ? team.EmblemPath : string.Empty;
        }

        public ObservableCollection<WrestlingMatch> UpcomingMatches
        {
            get { return _upcomingMatches; }
            set
            {
                _upcomingMatches = value;
                OnPropertyChanged("UpcomingMatches");
            }
        }

        public Carpet LastMatchCarpet
        {
            get { return _lastMatchCarpet; }
            set
            {
                _lastMatchCarpet = value;
                OnPropertyChanged("LastMatchCarpet");
            }
        }

        public Wrestler Winner
        {
            get { return _winner; }
            set
            {
                _winner = value;
                OnPropertyChanged("Winner");
            }
        }

        public SolidColorBrush WinnerColorBrush
        {
            get { return _winnerColorBrush; }
            set
            {
                _winnerColorBrush = value;
                OnPropertyChanged("WinnerColorBrush");
            }
        }

        public string WinnerTeamEmblem
        {
            get { return _winnerTeamEmblem; }
            set
            {
                _winnerTeamEmblem = value;
                OnPropertyChanged("WinnerTeamEmblem");
            }
        }

        public bool IsUpcomingMatchesVisible
        {
            get { return _isUpcomingMatchesVisible; }
            set
            {
                _isUpcomingMatchesVisible = value;
                OnPropertyChanged("IsUpcomingMatchesVisible");
            }
        }

        public bool IsMainScreenVisible
        {
            get { return _isMainScreenVisible; }
            set
            {
                _isMainScreenVisible = value;
                OnPropertyChanged("IsMainScreenVisible");
            }
        }

        public bool IsWinnerDialogVisible
        {
            get { return _isWinnerDialogVisible; }
            set
            {
                _isWinnerDialogVisible = value;
                OnPropertyChanged("IsWinnerDialogVisible");
            }
        }

        public string MatchFullNumber
        {
            get { return _matchFullNumber; }
            set
            {
                _matchFullNumber = value;
                OnPropertyChanged("MatchFullNumber");
            }
        }

        public bool IsPlayer1WithAdvantage
        {
            get { return _isPlayer1WithAdvantage; }
            set
            {
                _isPlayer1WithAdvantage = value;
                OnPropertyChanged("IsPlayer1WithAdvantage");
            }
        }

        public bool IsPlayer2WithAdvantage
        {
            get { return _isPlayer2WithAdvantage; }
            set
            {
                _isPlayer2WithAdvantage = value;
                OnPropertyChanged("IsPlayer2WithAdvantage");
            }
        }

        public bool IsLastActionRed
        {
            get { return _isLastActionRed; }
            set
            {
                _isLastActionRed = value;
                OnPropertyChanged("IsLastActionRed");
            }
        }

        public string Wrestler1TeamName
        {
            get { return _wrestler1TeamName; }
            set
            {
                _wrestler1TeamName = value;
                OnPropertyChanged("Wrestler1TeamName");
            }
        }

        public string Wrestler2TeamName
        {
            get { return _wrestler2TeamName; }
            set
            {
                _wrestler2TeamName = value;
                OnPropertyChanged("Wrestler2TeamName");
            }
        }

        public string Wrestler2TeamEmblem
        {
            get { return _wrestler2TeamEmblem; }
            set
            {
                _wrestler2TeamEmblem = value;
                OnPropertyChanged("Wrestler2TeamEmblem");
            }
        }

        public string Wrestler1TeamEmblem
        {
            get { return _wrestler1TeamEmblem; }
            set
            {
                _wrestler1TeamEmblem = value;
                OnPropertyChanged("Wrestler1TeamEmblem");
            }
        }

        public int BestActionRed
        {
            get { return _bestActionRed; }
            set
            {
                _bestActionRed = value;
                OnPropertyChanged("BestActionRed");
            }
        }

        public int BestActionBlue
        {
            get { return _bestActionBlue; }
            set
            {
                _bestActionBlue = value;
                OnPropertyChanged("BestActionBlue");
            }
        }

        public int MainSeconds
        {
            get { return _mainSeconds; }
            set
            {
                _mainSeconds = value;
                OnPropertyChanged("MainSeconds");
                OnPropertyChanged("TickCounter");
            }
        }

        public string RoundName
        {
            get { return _roundName; }
            set
            {
                _roundName = value;
                OnPropertyChanged("RoundName");
            }
        }


        public int SecondarySeconds
        {
            get { return _secondarySeconds; }
            set
            {
                _secondarySeconds = value;
                OnPropertyChanged("SecondarySeconds");
                OnPropertyChanged("TickCounterAction1");
                OnPropertyChanged("TickCounterAction2");
            }
        }

        public string TournamentTitle
        {
            get { return _tournamentTitle; }
            set
            {
                _tournamentTitle = value;
                OnPropertyChanged("TournamentTitle");
            }
        }

        public string CarpetLabel
        {
            get { return _carpetLabel; }
            set
            {
                _carpetLabel = value;
                OnPropertyChanged("CarpetLabel");
            }
        }

        public string GroupLabel
        {
            get { return _groupLabel; }
            set
            {
                _groupLabel = value;
                OnPropertyChanged("GroupLabel");
            }
        }

        public TimeSpan TickCounter => IsTimerBackward 
            ? new TimeSpan(0, 0, 0, (IsTimeout ? MaxTimeoutSecond : MaxRoundSecond) - MainSeconds) 
            : new TimeSpan(0, 0, 0, MainSeconds);

        public TimeSpan TickCounterAction1 => IsTimerBackward
            ? new TimeSpan(0, 0, 0, MaxActionSecond - SecondarySeconds)
            : new TimeSpan(0, 0, 0, SecondarySeconds);

        public TimeSpan TickCounterAction2 => IsTimerBackward
            ? new TimeSpan(0, 0, 0, MaxActionSecond - SecondarySeconds)
            : new TimeSpan(0, 0, 0, SecondarySeconds);

        public string Wrestler1
        {
            get { return _wrestler1; }
            set
            {
                _wrestler1 = value;
                OnPropertyChanged("Wrestler1");
            }
        }

        public string Wrestler2
        {
            get { return _wrestler2; }
            set
            {
                _wrestler2 = value;
                OnPropertyChanged("Wrestler2");
            }
        }

        public int Points1
        {
            get { return _points1; }
            set
            {
                _points1 = value;
                OnPropertyChanged("Points1");
            }
        }

        public int Points2
        {
            get { return _points2; }
            set
            {
                _points2 = value;
                OnPropertyChanged("Points2");
            }
        }

        public int MaxRoundSecond
        {
            get { return _maxRoundSecond; }
            set
            {
                _maxRoundSecond = value;
                OnPropertyChanged("MaxRoundSecond");
                OnPropertyChanged("TickCounter");
            }
        }

        public int MaxTimeoutSecond
        {
            get { return _maxTimeoutSecond; }
            set
            {
                _maxTimeoutSecond = value;
                OnPropertyChanged("MaxTimeoutSecond");
                OnPropertyChanged("TickCounter");
            }
        }

        public int MaxActionSecond
        {
            get { return _maxActionSecond; }
            set
            {
                _maxActionSecond = value;
                OnPropertyChanged("MaxActionSecond");
                OnPropertyChanged("TickCounterAction1");
                OnPropertyChanged("TickCounterAction2");
            }
        }

        public int Round
        {
            get { return _round; }
            set
            {
                _round = value;
                OnPropertyChanged("Round");
            }
        }

        public int Wrestler1WarningsNumber
        {
            get { return _wrestler1WarningsNumber; }
            set
            {
                if (value >= 3) _wrestler1WarningsNumber = 3;
                else if (value <= 0) _wrestler1WarningsNumber = 0;
                else
                {
                    _wrestler1WarningsNumber = value;
                }

                OnPropertyChanged("Wrestler1WarningsNumber");
            }
        }

        public int Wrestler2WarningsNumber
        {
            get { return _wrestler2WarningsNumber; }
            set
            {
                if (value >= 3) _wrestler2WarningsNumber = 3;
                else if (value <= 0) _wrestler2WarningsNumber = 0;
                else
                {
                    _wrestler2WarningsNumber = value;
                }
                
                OnPropertyChanged("Wrestler2WarningsNumber");
            }
        }

        public bool IsAction1TimerEnabled
        {
            get { return _isAction1TimerEnabled; }
            set
            {
                _isAction1TimerEnabled = value;
                OnPropertyChanged("IsAction1TimerEnabled");
            }
        }

        public bool IsAction2TimerEnabled
        {
            get { return _isAction2TimerEnabled; }
            set
            {
                _isAction2TimerEnabled = value;
                OnPropertyChanged("IsAction2TimerEnabled");
            }
        }

        public bool IsTimerBackward
        {
            get { return _isTimerBackward; }
            set
            {
                _isTimerBackward = value;
                OnPropertyChanged("IsTimerBackward");
            }
        }

        public bool IsSoundEnabled
        {
            get { return _isSoundEnabled; }
            set
            {
                _isSoundEnabled = value;
                OnPropertyChanged("IsSoundEnabled");
            }
        }

        public bool IsTimeout
        {
            get { return _isTimeout; }
            set
            {
                _isTimeout = value;
                OnPropertyChanged("IsTimeout");
                OnPropertyChanged("TickCounter");
            }
        }
    }
}