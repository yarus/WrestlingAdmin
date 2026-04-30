using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using MaterialDesignThemes.Wpf;
using Wrestling.Entities;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Material.Slider.Slides;
using Wrestling.UI.Material.Slider.Slides.CarpetBracketsSlide;
using Wrestling.UI.Material.Slider.Slides.GroupBracketSlide;
using Wrestling.UI.Material.Tournament;
using Wrestling.UI.Material.Tournament.Dashboard;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Slider
{
    public class SliderControlViewModel : TournamentViewModelBase
    {
        private ObservableCollection<AgeWeightGroup> _groups;
        private ObservableCollection<SlideChannel> _channels;
        private SlideChannel _selectedChannel;

        private ICommand _deleteSlideCommand;
        private ICommand _upSlideCommand;
        private ICommand _downSlideCommand;
        private ICommand _addSlideCommand;
        private ICommand _editSlideCommand;
        private ICommand _deleteAllSlidesCommand;

        private ICommand _addChannelCommand;
        private ICommand _renameChannelCommand;
        private ICommand _deleteChannelCommand;
        private ICommand _toggleChannelCommand;

        private ISliderWindowManager _windowManager;

        // One preview VM per channel so the preview timer keeps running across
        // channel toggles. Also lets users park multiple previews (e.g. channel
        // A rotating, channel B rotating) without losing state when switching.
        private readonly Dictionary<SlideChannel, SlideHostViewModel> _previewVms = new Dictionary<SlideChannel, SlideHostViewModel>();
        private Entities.Tournament _cachedTournament;

        private IList<CommandButtonItem> _quickButtons;

        public SliderControlViewModel(IDiContainer container) : base(container)
        {
        }

        public override string PageTitle => "Управление слайдером";

        public override void InitData()
        {
            base.InitData();

            _windowManager = Resolve<ISliderWindowManager>();

            // Entering the page with a different tournament than last time means
            // cached preview VMs are bound to orphaned channels. Tear them down
            // so we don't leak timers into the new session.
            if (!ReferenceEquals(_cachedTournament, DataContext.Tournament))
            {
                foreach (var vm in _previewVms.Values) vm.Shutdown();
                _previewVms.Clear();
                _cachedTournament = DataContext.Tournament;
            }

            _groups = DataContext.Tournament.Groups;

            Channels = DataContext.Tournament.SlideChannels;

            if (Channels.Count == 0)
            {
                CreateDefaultChannel();
            }

            SelectedChannel = Channels.FirstOrDefault();
        }

        // Intentionally no OnNavigatingOut cleanup: users expect a preview
        // they started to keep rotating while they step away to the dashboard
        // and come back. The cache is only torn down when the tournament
        // reference changes (handled in InitData) or the owning channel is
        // deleted (handled in DeleteChannel).

        public override IList<CommandButtonItem> QuickButtons
        {
            get
            {
                return _quickButtons ??
                (
                    _quickButtons = new List<CommandButtonItem>
                    {
                        new CommandButtonItem("Новый канал", PackIconKind.PlaylistPlus, new RelayCommand(param => AddChannel(), param => true)),
                        new CommandButtonItem("Остановить все таймеры", PackIconKind.TimerOff, new RelayCommand(param => StopAllTimers(), param => HasAnyRunningTimer())),
                        new CommandButtonItem("Закрыть все слайдеры", PackIconKind.CloseCircleOutline, new RelayCommand(param => CloseAllSliders(), param => _windowManager?.OpenCount > 0))
                    }
                );
            }
        }

        public override bool IsBackButtonAvailable => true;

        protected override void OnBackCommand()
        {
            base.OnBackCommand();

            NavigateToView<DashboardViewModel>();
        }

        public ObservableCollection<SlideChannel> Channels
        {
            get { return _channels; }
            set
            {
                _channels = value;
                OnPropertyChanged("Channels");
            }
        }

        public SlideChannel SelectedChannel
        {
            get { return _selectedChannel; }
            set
            {
                if (_selectedChannel != null)
                {
                    _selectedChannel.PropertyChanged -= OnSelectedChannelPropertyChanged;
                    if (_selectedChannel.Slides != null)
                    {
                        _selectedChannel.Slides.CollectionChanged -= OnSelectedChannelSlidesChanged;
                    }
                }

                _selectedChannel = value;

                if (_selectedChannel != null)
                {
                    _selectedChannel.PropertyChanged += OnSelectedChannelPropertyChanged;
                    if (_selectedChannel.Slides != null)
                    {
                        _selectedChannel.Slides.CollectionChanged += OnSelectedChannelSlidesChanged;
                    }
                }

                OnPropertyChanged("SelectedChannel");
                OnPropertyChanged("Slides");
                OnPropertyChanged("Host");
                OnPropertyChanged("HasSelectedChannel");
                OnPropertyChanged("CanToggleTimer");
            }
        }

        // Refresh CanToggleTimer whenever the selected channel gains or loses
        // slides — the Прев toggle greys out the moment the list empties and
        // re-enables when the first slide appears.
        private void OnSelectedChannelSlidesChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged("CanToggleTimer");
        }

        // Bound to the Прев ToggleButton's IsEnabled. A channel with no slides
        // has nothing to rotate, so the switch is locked off until at least
        // one slide is added.
        public bool CanToggleTimer => _selectedChannel?.Slides?.Count > 0;

        // When the selected channel's window opens or closes, Host flips
        // between the live window VM and the local preview VM — refresh the
        // bindings so the Прев toggle and ListView reflect whichever is active.
        private void OnSelectedChannelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SlideChannel.IsOpen))
            {
                OnPropertyChanged("Host");
            }
        }

        public bool HasSelectedChannel => _selectedChannel != null;

        // When a monitor window is open for the selected channel, Host mirrors
        // that window's VM so the in-panel timer toggle + current-slide
        // selection reflect the live state. Otherwise it returns a per-channel
        // preview VM (cached so its timer state survives channel switches).
        public SlideHostViewModel Host
        {
            get
            {
                if (_selectedChannel == null) return null;
                return _windowManager?.GetViewModelForChannel(_selectedChannel) ?? GetOrCreatePreview(_selectedChannel);
            }
        }

        private SlideHostViewModel GetOrCreatePreview(SlideChannel channel)
        {
            if (!_previewVms.TryGetValue(channel, out var vm))
            {
                vm = new SlideHostViewModel(DiContainer) { Channel = channel };
                vm.InitData();
                _previewVms[channel] = vm;
            }
            return vm;
        }

        // ListView ItemsSource — tracks the selected channel's slides.
        public ObservableCollection<ScreenSlide> Slides => _selectedChannel?.Slides;

        #region Slide commands (scoped to SelectedChannel)

        public ICommand DeleteAllSlidesCommand
        {
            get
            {
                if (_deleteAllSlidesCommand == null)
                {
                    _deleteAllSlidesCommand = new RelayCommand(param => DeleteAllSlides(), param => _selectedChannel != null);
                }
                return _deleteAllSlidesCommand;
            }
        }

        public ICommand DeleteSlideCommand
        {
            get
            {
                if (_deleteSlideCommand == null)
                {
                    _deleteSlideCommand = new RelayCommand(param => DeleteSlide(param as ScreenSlide), param => param != null);
                }
                return _deleteSlideCommand;
            }
        }

        public ICommand UpSlideCommand
        {
            get
            {
                if (_upSlideCommand == null)
                {
                    _upSlideCommand = new RelayCommand(param => UpSlide(param as ScreenSlide), param => param != null);
                }
                return _upSlideCommand;
            }
        }

        public ICommand DownSlideCommand
        {
            get
            {
                if (_downSlideCommand == null)
                {
                    _downSlideCommand = new RelayCommand(param => DownSlide(param as ScreenSlide), param => param != null);
                }
                return _downSlideCommand;
            }
        }

        public ICommand AddSlideCommand
        {
            get
            {
                if (_addSlideCommand == null)
                {
                    _addSlideCommand = new RelayCommand(param => AddSlide(), param => _selectedChannel != null);
                }
                return _addSlideCommand;
            }
        }

        public ICommand EditSlideCommand
        {
            get
            {
                if (_editSlideCommand == null)
                {
                    _editSlideCommand = new RelayCommand(param => EditSlide(param as ScreenSlide), param => param != null);
                }
                return _editSlideCommand;
            }
        }

        #endregion

        #region Channel commands

        public ICommand AddChannelCommand
        {
            get
            {
                if (_addChannelCommand == null)
                {
                    _addChannelCommand = new RelayCommand(param => AddChannel(), param => true);
                }
                return _addChannelCommand;
            }
        }

        public ICommand RenameChannelCommand
        {
            get
            {
                if (_renameChannelCommand == null)
                {
                    _renameChannelCommand = new RelayCommand(param => RenameChannel(param as SlideChannel), param => param is SlideChannel);
                }
                return _renameChannelCommand;
            }
        }

        public ICommand DeleteChannelCommand
        {
            get
            {
                if (_deleteChannelCommand == null)
                {
                    _deleteChannelCommand = new RelayCommand(param => DeleteChannel(param as SlideChannel), param => param is SlideChannel);
                }
                return _deleteChannelCommand;
            }
        }

        public ICommand ToggleChannelCommand
        {
            get
            {
                if (_toggleChannelCommand == null)
                {
                    _toggleChannelCommand = new RelayCommand(param => ToggleChannel(param as SlideChannel), param => param is SlideChannel);
                }
                return _toggleChannelCommand;
            }
        }

        #endregion

        private async void ToggleChannel(SlideChannel channel)
        {
            if (channel == null) return;

            // One button per channel: open on a chosen monitor if closed, close
            // the existing window if open. This matches the single on-screen
            // control the user sees, without a separate "close" action.
            if (_windowManager.CountForChannel(channel) > 0)
            {
                _windowManager.CloseChannel(channel);
                return;
            }

            var monitor = await MonitorPicker.PickAsync();
            if (monitor == null) return;

            _windowManager.OpenOnMonitor(channel, monitor);
        }

        private void CloseAllSliders()
        {
            _windowManager.CloseAll();
        }

        // Turns off rotation timers across every channel — cached preview VMs
        // and any open monitor-window VMs. Windows remain open on their current
        // slide; the user can re-enable rotation per channel via the Прев
        // toggle on the detail pane.
        private void StopAllTimers()
        {
            foreach (var vm in _previewVms.Values)
            {
                vm.IsTimerEnabled = false;
            }

            _windowManager.StopAllTimers();
        }

        private bool HasAnyRunningTimer()
        {
            if (_previewVms.Values.Any(vm => vm.IsTimerEnabled)) return true;
            return _windowManager?.HasAnyRunningTimer() ?? false;
        }

        private async void AddChannel()
        {
            var channel = new SlideChannel
            {
                Name = NextChannelName(),
                SliderMaxSecond = DataContext.Tournament.Settings.SliderMaxSecond
            };

            var vm = new ChannelEditorViewModel
            {
                Name = channel.Name,
                SliderMaxSecond = channel.SliderMaxSecond
            };

            var dialog = new ChannelEditorDialog { DataContext = vm };
            var result = await DialogHost.Show(dialog, "RootDialog");
            if (result is bool ok && ok)
            {
                channel.Name = string.IsNullOrWhiteSpace(vm.Name) ? channel.Name : vm.Name.Trim();
                channel.SliderMaxSecond = vm.SliderMaxSecond > 0 ? vm.SliderMaxSecond : channel.SliderMaxSecond;

                Channels.Add(channel);
                SelectedChannel = channel;
            }
        }

        private async void RenameChannel(SlideChannel channel)
        {
            if (channel == null) return;

            var vm = new ChannelEditorViewModel
            {
                Name = channel.Name,
                SliderMaxSecond = channel.SliderMaxSecond
            };

            var dialog = new ChannelEditorDialog { DataContext = vm };
            var result = await DialogHost.Show(dialog, "RootDialog");
            if (result is bool ok && ok)
            {
                if (!string.IsNullOrWhiteSpace(vm.Name)) channel.Name = vm.Name.Trim();
                if (vm.SliderMaxSecond > 0) channel.SliderMaxSecond = vm.SliderMaxSecond;
            }
        }

        private void DeleteChannel(SlideChannel channel)
        {
            if (channel == null) return;

            if (Dialog.ShowMessageBox(this, $"Удалить канал «{channel.Name}» со всеми слайдами?", "Требуется подтверждение", MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK) return;

            _windowManager.CloseChannel(channel);

            if (_previewVms.TryGetValue(channel, out var previewVm))
            {
                previewVm.Shutdown();
                _previewVms.Remove(channel);
            }

            Channels.Remove(channel);

            if (Channels.Count == 0)
            {
                CreateDefaultChannel();
            }

            if (SelectedChannel == channel)
            {
                SelectedChannel = Channels.FirstOrDefault();
            }
        }

        private async void EditSlide(ScreenSlide slide)
        {
            var tmpSlide = slide.Clone() as ScreenSlide;

            var vm = new AddSlideViewModel(DiContainer)
            {
                Item = tmpSlide
            };

            var view = new AddSlideDialog
            {
                DataContext = vm
            };

            vm.InitData();

            var result = await DialogHost.Show(view, "RootDialog");

            if (result != null && (bool)result)
            {
                slide.Sync(tmpSlide);
            }
        }

        private async void AddSlide()
        {
            if (_selectedChannel == null) return;

            var defaultDuration = _selectedChannel.SliderMaxSecond > 0
                ? _selectedChannel.SliderMaxSecond
                : DataContext.Tournament.Settings.SliderMaxSecond;

            var vm = new AddSlideViewModel(DiContainer)
            {
                Item = new ScreenSlide { Duration = defaultDuration }
            };

            var view = new AddSlideDialog
            {
                DataContext = vm
            };

            vm.InitData();

            var result = await DialogHost.Show(view, "RootDialog");

            if (result != null && (bool)result)
            {
                if (vm.Item.SlideType == CarpetBracketsSlide.TypeName)
                {
                    // Macro: expand to one regular GroupBracketSlide per group
                    // of the chosen carpet (with dedup by GroupID). The macro
                    // itself is never persisted into the channel.
                    ExpandCarpetBracketsMacro(vm.Item);
                    return;
                }

                // In-place mutation: open slide-host windows and the cached
                // preview VM all share this ObservableCollection reference,
                // so the new slide shows up in the next rotation without
                // forcing a state-destroying InitData().
                _selectedChannel.Slides.Add(vm.Item);
            }
        }

        private void ExpandCarpetBracketsMacro(ScreenSlide macro)
        {
            if (_selectedChannel == null) return;

            var carpetIdRaw = macro.GetNamedValue("CarpetID");
            if (carpetIdRaw == null) return;

            Guid carpetId;
            try { carpetId = new Guid(carpetIdRaw.ToString()); }
            catch { return; }

            var carpet = DataContext.Tournament.Carpets.FirstOrDefault(c => c.ID == carpetId);
            if (carpet == null) return;

            var groupBracketTypeName = Resolve<List<ISlideType>>()
                .OfType<GroupBracketSlide>()
                .Select(t => t.SlideType)
                .FirstOrDefault();
            if (string.IsNullOrEmpty(groupBracketTypeName)) return;

            var existingGroupIds = new HashSet<Guid>(
                _selectedChannel.Slides
                    .Where(s => s.SlideType == groupBracketTypeName)
                    .Select(s => s.GetNamedValue("GroupID"))
                    .Where(v => v != null)
                    .Select(v => { try { return (Guid?)new Guid(v.ToString()); } catch { return null; } })
                    .Where(g => g.HasValue)
                    .Select(g => g.Value));

            foreach (var group in carpet.Groups)
            {
                if (!existingGroupIds.Add(group.ID)) continue;

                var slide = new ScreenSlide
                {
                    Title = group.Name,
                    SlideType = groupBracketTypeName,
                    Duration = macro.Duration
                };
                slide.NamedValues.Add("GroupID", group.ID);

                _selectedChannel.Slides.Add(slide);
            }
        }

        private void UpSlide(ScreenSlide slide)
        {
            if (_selectedChannel == null) return;

            var slides = _selectedChannel.Slides;
            var i = slides.IndexOf(slide);
            var j = i - 1;
            if (j >= 0)
            {
                slides.Swap(i, j);
            }
        }

        private void DownSlide(ScreenSlide slide)
        {
            if (_selectedChannel == null) return;

            var slides = _selectedChannel.Slides;
            var i = slides.IndexOf(slide);
            var j = i + 1;
            if (j < slides.Count)
            {
                slides.Swap(i, j);
            }
        }

        private void DeleteAllSlides()
        {
            if (_selectedChannel == null) return;

            if (Dialog.ShowMessageBox(this, "Вы уверены, что хотите удалить все слайды?", "Требуется подтверждение", MessageBoxButton.OKCancel, MessageBoxImage.Information) != MessageBoxResult.OK) return;

            // Clear in place — replacing the collection would invalidate cached
            // Slides references held by the preview VM and open window VMs.
            _selectedChannel.Slides.Clear();
        }

        private void DeleteSlide(ScreenSlide slide)
        {
            if (_selectedChannel == null) return;

            _selectedChannel.Slides.Remove(slide);
        }

        private string NextChannelName()
        {
            int i = Channels.Count + 1;
            string candidate;
            do
            {
                candidate = $"Канал {i}";
                i++;
            } while (Channels.Any(c => c.Name == candidate));
            return candidate;
        }

        private void CreateDefaultChannel()
        {
            var groupBracketSlide = new GroupBracketSlide(DiContainer);
            var channel = new SlideChannel
            {
                Name = "Основной",
                SliderMaxSecond = DataContext.Tournament.Settings.SliderMaxSecond
            };

            foreach (var group in _groups)
            {
                var slide = new ScreenSlide
                {
                    Title = group.Name,
                    SlideType = groupBracketSlide.SlideType,
                    Duration = DataContext.Tournament.Settings.SliderMaxSecond
                };

                slide.NamedValues.Add("GroupID", group.ID);

                channel.Slides.Add(slide);
            }

            Channels.Add(channel);
        }
    }
}
