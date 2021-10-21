using System.Windows;
using System.Windows.Controls;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Utils;

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
                if (!VisualPrinter.PrintAcrossPages(dlg, PrintControl, "Печать"))
                {
                    MessageBox.Show(this, "Ошибка печати. Попробуйте еще раз.");
                }
            }
        }
    }
}