using System;
using MaterialDesignThemes.Wpf;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material
{
    public class MainWindowViewModel : ViewModelBase, IShellViewModel
    {
        private ViewModelBase _currentViewModel;
        private readonly ISnackbarMessageQueue _snackbarMessageQueue;

        public MainWindowViewModel(ISnackbarMessageQueue snackbarMessageQueue, IDiContainer di) : base(di)
        {
            if (snackbarMessageQueue == null) throw new ArgumentNullException(nameof(snackbarMessageQueue));

            _snackbarMessageQueue = snackbarMessageQueue;
        }

        public event EventHandler OnRequestClose;

        public void ShowSnackbarMessage(string message)
        {
            _snackbarMessageQueue.Enqueue(message);
        }
        
        public void RequestClose()
        {
            OnRequestClose?.Invoke(this, new EventArgs());
        }

        public ViewModelBase CurrentViewModel
        {
            get
            {
                return _currentViewModel;
            }
            set
            {
                if (_currentViewModel != value)
                {
                    _currentViewModel = value;
                    OnPropertyChanged("CurrentViewModel");
                }
            }
        }
    }
}