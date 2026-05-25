using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using MaterialDesignThemes.Wpf;
using Wrestling.Entities;
using Wrestling.UI.Utils.Localization;

namespace Wrestling.UI.Material.Tournament.Print
{
    public partial class PartSelectorDialog : UserControl
    {
        public PartSelectorDialog()
        {
            InitializeComponent();
        }

        // Returns the chosen TournamentPart (or null when "All parts" was
        // picked / there's only one part and no dialog was shown).
        // Returns a tuple (Confirmed, Part) — Confirmed=false when the user
        // cancelled; callers should abort their export in that case.
        public static async Task<(bool Confirmed, TournamentPart Part)> PromptAsync(Entities.Tournament tournament)
        {
            if (tournament?.Parts == null || tournament.Parts.Count <= 1)
            {
                // Trivial case: only one part exists. No need for a dialog;
                // export the single part directly (Part returned so callers
                // can still scope by PartID consistently).
                return (true, tournament?.Parts?.FirstOrDefault());
            }

            var vm = new PartSelectorDialogViewModel(tournament);
            var view = new PartSelectorDialog { DataContext = vm };
            var result = await DialogHost.Show(view, "RootDialog");

            if (!(result is bool ok) || !ok) return (false, null);
            return (true, vm.SelectedOption?.Part);
        }
    }

    // VM for the dialog. Pre-populates with a "Все части" sentinel followed
    // by every part. Default-selects the first non-empty part so the most
    // useful option is highlighted out of the gate.
    public sealed class PartSelectorDialogViewModel : Wrestling.Entities.ObservableObject
    {
        private PartOption _selectedOption;

        public PartSelectorDialogViewModel(Entities.Tournament tournament)
        {
            Options = new List<PartOption>();
            Options.Add(new PartOption { Part = null, Label = ProviderLocalization("BulkExport_PartSelector_All", "Все части") });
            if (tournament?.Parts != null)
            {
                foreach (var p in tournament.Parts)
                {
                    Options.Add(new PartOption { Part = p, Label = p.Name });
                }
            }

            // Default selection: first non-empty part, otherwise first part,
            // otherwise the "All" sentinel.
            var firstNonEmpty = tournament?.Parts?.FirstOrDefault(p =>
                tournament.Groups.Any(g => g.PartID == p.ID && g.Bracket != null));
            _selectedOption = Options.FirstOrDefault(o => o.Part == firstNonEmpty)
                ?? Options[Options.Count > 1 ? 1 : 0];
        }

        public List<PartOption> Options { get; }

        public PartOption SelectedOption
        {
            get => _selectedOption;
            set { _selectedOption = value; OnPropertyChanged(nameof(SelectedOption)); }
        }

        private static string ProviderLocalization(string key, string fallback)
        {
            var v = LocalizationService.Instance?.T(key);
            return string.IsNullOrEmpty(v) || v == key ? fallback : v;
        }
    }

    public sealed class PartOption
    {
        public TournamentPart Part { get; set; }
        public string Label { get; set; }
    }
}
