using System;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Wrestling.UI.Utils
{
    public class AsyncRelayCommand : ICommand
    {
        private readonly Func<object, Task> _execute;
        private readonly Predicate<object> _canExecute;
        private bool _isExecuting;

        #region Constructors

        public AsyncRelayCommand(Func<object, Task> execute)
            : this(execute, null)
        {
        }

        public AsyncRelayCommand(Func<object, Task> execute, Predicate<object> canExecute)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        #endregion

        #region ICommand Members

        public bool CanExecute(object parameter)
        {
            return !_isExecuting && (_canExecute?.Invoke(parameter) ?? true);
        }

        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public async void Execute(object parameter)
        {
            if (!CanExecute(parameter)) return;

            _isExecuting = true;
            RaiseCanExecuteChanged();
            try
            {
                await _execute(parameter).ConfigureAwait(true); // Return to UI context
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AsyncRelayCommand execution failed: {ex}");

                // Re-post to the Dispatcher so Application.DispatcherUnhandledException
                // sees it synchronously, instead of the GC-delayed TaskScheduler.UnobservedTaskException
                // path that kicks in if the command was started without a captured SynchronizationContext.
                var dispatcher = Application.Current?.Dispatcher;
                if (dispatcher != null)
                {
                    var capture = ExceptionDispatchInfo.Capture(ex);
                    _ = dispatcher.BeginInvoke(new Action(() => capture.Throw()));
                }
                else
                {
                    throw;
                }
            }
            finally
            {
                _isExecuting = false;
                RaiseCanExecuteChanged();
            }
        }

        #endregion

        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
