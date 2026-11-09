using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Wrestling.Entities;
using Wrestling.UI.Material.Tournament;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Slider.Slides.TournamentUpcomingMatchesSlide
{
    public sealed class TournamentUpcomingMatchesViewModel : TournamentViewModelBase, ISliderViewControl
    {
        private ScreenSlide _slide;

        public TournamentUpcomingMatchesViewModel(IDiContainer di) : base(di) { }

        public string Header => T("Slide_TournamentUpcoming_Header", "Ближайшие схватки турнира");
        public ObservableCollection<UpcomingMatColumn> Columns { get; } = new ObservableCollection<UpcomingMatColumn>();
        public int ShowMatchesCount { get; private set; }
        public double MatColumnWidth => Math.Max(1, 1860.0 / Math.Max(1, Columns.Count) - 20);
        // The complete 720 x 150 card (including its gap) scales uniformly.
        private double CardScale => Math.Min(MatColumnWidth / 720, 884.0 / (Math.Max(1, ShowMatchesCount) * 150));
        public double CardWidth => 720 * CardScale;
        public double CardHeight => 150 * CardScale;
        public double QueueHeight => CardHeight * ShowMatchesCount;
        public string SliderBackgroundImagePath { get; private set; }
        public double SliderOpacity { get; private set; }
        public bool HasNoColumns => Columns.Count == 0;

        internal static HashSet<Guid> ReadMatIds(ScreenSlide slide) =>
            (slide.GetNamedValue("MatIDs")?.ToString() ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => Guid.TryParse(s, out var id) ? id : Guid.Empty)
                .Where(id => id != Guid.Empty).ToHashSet();

        public void InitContext(ScreenSlide slide)
        {
            _slide = slide;
            InitData();
            ShowMatchesCount = Math.Max(1, Convert.ToInt32(slide.GetNamedValue("ShowMatchesCount") ?? 4));
            SliderBackgroundImagePath = slide.GetNamedValue("SliderBackgroundImagePath")?.ToString()
                ?? Tournament?.Settings.SliderBackgroundImagePath;
            SliderOpacity = Math.Clamp(Convert.ToDouble(slide.GetNamedValue("SliderOpacityValue")
                ?? Tournament?.Settings.SliderOpacityValue ?? 0) / 100, 0, 1);
            RefreshColumns();
            OnPropertyChanged(nameof(ShowMatchesCount));
            OnPropertyChanged(nameof(SliderBackgroundImagePath));
            OnPropertyChanged(nameof(SliderOpacity));
        }

        public void TimerTick() => RefreshColumns();

        private void RefreshColumns()
        {
            var ids = ReadMatIds(_slide);
            var all = _slide.GetNamedValue("MatIDs") == null;
            var mats = Tournament?.Mats.Where(m => all || (m.ID.HasValue && ids.Contains(m.ID.Value))).ToList()
                ?? new List<Mat>();
            // Keep columns (including empty mats) in tournament order and retain rows
            // when the queue has not changed, avoiding a redraw on every timer tick.
            if (!Columns.Select(c => c.Mat).SequenceEqual(mats))
            {
                Columns.Clear();
                foreach (var mat in mats) Columns.Add(new UpcomingMatColumn(mat));
            }
            foreach (var column in Columns)
            {
                var matches = column.Mat.Groups
                    .Where(g => (!column.Mat.ActivePartID.HasValue || g.PartID == column.Mat.ActivePartID) && g.Bracket != null)
                    .SelectMany(g => g.Bracket.Rounds).SelectMany(r => r.RoundMatches)
                    .Where(m => m.IsMatchCanStart).OrderBy(m => m.MatchNumber).Take(ShowMatchesCount).ToList();
                column.Update(matches);
            }
            OnPropertyChanged(nameof(MatColumnWidth));
            OnPropertyChanged(nameof(CardWidth));
            OnPropertyChanged(nameof(CardHeight));
            OnPropertyChanged(nameof(QueueHeight));
            OnPropertyChanged(nameof(HasNoColumns));
        }
    }

    public sealed class UpcomingMatColumn : ObservableObject
    {
        public UpcomingMatColumn(Mat mat) { Mat = mat; }
        public Mat Mat { get; }
        public ObservableCollection<WrestlingMatch> Matches { get; } = new ObservableCollection<WrestlingMatch>();
        public bool IsEmpty => Matches.Count == 0;

        internal void Update(IReadOnlyList<WrestlingMatch> matches)
        {
            if (Matches.SequenceEqual(matches)) return;
            Matches.Clear();
            foreach (var match in matches) Matches.Add(match);
            OnPropertyChanged(nameof(IsEmpty));
        }
    }
}
