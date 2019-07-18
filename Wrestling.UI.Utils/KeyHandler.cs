using System;
using System.Windows.Input;

namespace Wrestling.UI.Utils
{
    public class KeyHandler : IKeyHandler
    {
        public void RiseKeyDown(KeyEventArgs e)
        {
            KeyPressed?.Invoke(this, e);
        }

        public event EventHandler<KeyEventArgs> KeyPressed;
    }
}