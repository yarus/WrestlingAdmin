using System;
using System.Windows.Input;

namespace Wrestling.UI.Utils
{
    public interface IKeyHandler
    {
        event EventHandler<KeyEventArgs> KeyPressed;

        void RiseKeyDown(KeyEventArgs e);
    }
}