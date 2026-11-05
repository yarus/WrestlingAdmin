using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;
using Wrestling.Entities;
using Wrestling.UI.Material.Model;

namespace Wrestling.UI.Material.Match
{
    // Shared indicator for the schedule row and the compact bracket cell.
    public sealed class MatchActivityIndicator : StackPanel
    {
        public static readonly DependencyProperty MatchProperty = DependencyProperty.Register(nameof(Match),
            typeof(WrestlingMatch), typeof(MatchActivityIndicator), new PropertyMetadata(null, OnSourceChanged));
        public static readonly DependencyProperty ActivityProperty = DependencyProperty.Register(nameof(Activity),
            typeof(MatchActivityService), typeof(MatchActivityIndicator), new PropertyMetadata(null, OnSourceChanged));
        public static readonly DependencyProperty ShowLabelProperty = DependencyProperty.Register(nameof(ShowLabel),
            typeof(bool), typeof(MatchActivityIndicator), new PropertyMetadata(false, OnSourceChanged));

        private readonly TextBlock _label;
        private readonly PackIcon _icon;
        private MatchActivityService _subscribedActivity;
        private WrestlingMatch _subscribedMatch;

        public WrestlingMatch Match { get => (WrestlingMatch)GetValue(MatchProperty); set => SetValue(MatchProperty, value); }
        public MatchActivityService Activity { get => (MatchActivityService)GetValue(ActivityProperty); set => SetValue(ActivityProperty, value); }
        public bool ShowLabel { get => (bool)GetValue(ShowLabelProperty); set => SetValue(ShowLabelProperty, value); }

        public MatchActivityIndicator()
        {
            Orientation = Orientation.Horizontal;
            VerticalAlignment = VerticalAlignment.Center;
            Background = Brushes.Transparent;
            _icon = new PackIcon { Kind = PackIconKind.AlertCircleOutline, Width = 16, Height = 16,
                Foreground = new SolidColorBrush(Color.FromRgb(0xF9, 0xA8, 0x25)), VerticalAlignment = VerticalAlignment.Center };
            Children.Add(_icon);
            _label = new TextBlock { Margin = new Thickness(5, 0, 0, 0), MaxWidth = 96,
                Foreground = _icon.Foreground, FontWeight = FontWeights.Bold,
                TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
            Children.Add(_label);
            ToolTipService.SetInitialShowDelay(this, 0);
            ToolTipService.SetShowDuration(this, 30000);
            Loaded += (_, _) => Rebind();
            Unloaded += (_, _) => Detach();
            Visibility = Visibility.Collapsed;
        }

        private static void OnSourceChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
            => ((MatchActivityIndicator)sender).Rebind();

        private void Detach()
        {
            if (_subscribedActivity != null) _subscribedActivity.Changed -= Refresh;
            if (_subscribedMatch != null) _subscribedMatch.PropertyChanged -= OnMatchChanged;
            _subscribedActivity = null;
            _subscribedMatch = null;
        }

        private void Rebind()
        {
            Detach();
            if (IsLoaded)
            {
                _subscribedActivity = Activity;
                _subscribedMatch = Match;
                if (_subscribedActivity != null) _subscribedActivity.Changed += Refresh;
                if (_subscribedMatch != null) _subscribedMatch.PropertyChanged += OnMatchChanged;
            }
            Refresh();
        }

        private void OnMatchChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(WrestlingMatch.Status)) Refresh();
        }

        private void Refresh()
        {
            var status = Activity?.GetStatus(Match) ?? MatchActivityStatus.Empty;
            // GridView measures Auto columns before the first advertisement arrives.
            // Reserve the schedule slot even while empty; compact bracket icons collapse.
            Visibility = status.IsOccupied ? Visibility.Visible : ShowLabel ? Visibility.Hidden : Visibility.Collapsed;
            _label.Visibility = ShowLabel ? Visibility.Visible : Visibility.Collapsed;
            _icon.Width = _icon.Height = ShowLabel ? 32 : 16;
            _label.Text = status.Label;
            ToolTip = status.ToolTip;
        }
    }
}
