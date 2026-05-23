using System.Collections.Generic;
using System.Linq;
using Wrestling.Entities;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Slider
{
    public class SliderWindowManager : ISliderWindowManager
    {
        private readonly IDiContainer _container;
        private readonly List<Entry> _entries = new List<Entry>();

        private class Entry
        {
            public SlideHostView View { get; set; }
            public SlideHostViewModel ViewModel { get; set; }
            public SlideChannel Channel { get; set; }
        }

        public SliderWindowManager(IDiContainer container)
        {
            _container = container;
        }

        public int OpenCount => _entries.Count;

        public int CountForChannel(SlideChannel channel) => _entries.Count(e => e.Channel == channel);

        public SlideHostViewModel GetViewModelForChannel(SlideChannel channel)
            => _entries.FirstOrDefault(e => e.Channel == channel)?.ViewModel;

        public void OpenOnMonitor(SlideChannel channel, System.Windows.Forms.Screen monitor)
        {
            // One window per channel. If the channel already has a window open,
            // surface it rather than spawning a duplicate on another monitor.
            var existing = _entries.FirstOrDefault(e => e.Channel == channel);
            if (existing != null)
            {
                existing.View.Activate();
                return;
            }

            var vm = new SlideHostViewModel(_container) { Channel = channel };
            vm.InitData();

            // A slide-host window put on a projection monitor is expected to
            // rotate automatically — the user opened it to cycle. (The preview
            // inside SliderControl stays user-toggled via the ToggleButton.)
            vm.IsTimerEnabled = true;

            var view = new SlideHostView { TargetMonitor = monitor };

            var entry = new Entry { View = view, ViewModel = vm, Channel = channel };
            _entries.Add(entry);

            view.Closed += (s, e) =>
            {
                vm.Shutdown();
                _entries.Remove(entry);
                channel.IsOpen = false;
            };

            channel.IsOpen = true;
            view.ShowScreen(vm);
        }

        public void RefreshChannel(SlideChannel channel)
        {
            foreach (var entry in _entries.Where(e => e.Channel == channel))
            {
                entry.ViewModel.InitData();
            }
        }

        public void CloseChannel(SlideChannel channel)
        {
            var snapshot = _entries.Where(e => e.Channel == channel).ToList();
            foreach (var entry in snapshot)
            {
                entry.View.Close();
            }
        }

        public void CloseAll()
        {
            var snapshot = _entries.ToList();
            foreach (var entry in snapshot)
            {
                entry.View.Close();
            }
        }

        public void StopAllTimers()
        {
            foreach (var entry in _entries)
            {
                entry.ViewModel.IsTimerEnabled = false;
            }
        }

        public bool HasAnyRunningTimer() => _entries.Any(e => e.ViewModel.IsTimerEnabled);
    }
}
