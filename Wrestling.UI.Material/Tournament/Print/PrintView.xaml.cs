using System.Windows;
using System.Windows.Controls;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Utils;
using Wrestling.UI.Utils.Localization;

namespace Wrestling.UI.Material.Tournament.Print
{
    public partial class PrintView : ChildWindowBase
    {
        public PrintView()
        {
            InitializeComponent();
        }

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            var dlg = new PrintDialog();
            if ((bool)dlg.ShowDialog())
            {
                // documentName intentionally kept as a literal Russian "Печать"
                // — Microsoft Print to PDF (and some other drivers) corrupt
                // cyrillic in this field. See docs/PrintingNotes.md.
                if (!VisualPrinter.PrintAcrossPages(dlg, PrintControl, "Печать"))
                {
                    var msg = LocalizationService.Instance?.T("Print_Error");
                    if (string.IsNullOrEmpty(msg) || msg == "Print_Error") msg = "Ошибка печати. Попробуйте еще раз.";
                    MessageBox.Show(this, msg);
                }
            }
        }
    }
}