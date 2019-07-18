using System;
using System.Windows.Controls;

namespace Wrestling.UI.Material.Utils
{
    public class PrintRequestedEventArgs : EventArgs
    {
        public PrintDialog Dialog { get; }

        public PrintRequestedEventArgs(PrintDialog dialog)
        {
            Dialog = dialog;
        }
    }
}