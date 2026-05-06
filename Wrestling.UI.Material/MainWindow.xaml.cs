using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using MvvmDialogs;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly IDiContainer _di;

        public MainWindow(IDiContainer di)
        {
            InitializeComponent();

            _di = di;

            // Show the welcome snackbar a short moment after the shell appears,
            // without burning a threadpool thread on Thread.Sleep.
            var welcome = new DispatcherTimer(DispatcherPriority.ApplicationIdle)
            {
                Interval = TimeSpan.FromMilliseconds(2500)
            };
            welcome.Tick += (_, _) =>
            {
                welcome.Stop();
                MainSnackbar.MessageQueue.Enqueue("Добро пожаловать в Администратор турниров по вольной борьбе!");
            };
            welcome.Start();

            var vm = new MainWindowViewModel(MainSnackbar.MessageQueue, _di);
            vm.OnRequestClose += ViewModelRequestClose;
            vm.InitData();

            DataContext = vm;
        }

        private void ViewModelRequestClose(object sender, System.EventArgs e)
        {
            Close();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            // show the message box here and collect the result
            var dialogService = _di.Resolve<IDialogService>();

            if (dialogService.ShowMessageBox(DataContext as MainWindowViewModel,
                    "Вы уверены что хотите закрыть приложение? Все несохраненные данные будут утеряны!",
                    "Требуется подтверждение", MessageBoxButton.OKCancel, MessageBoxImage.Information) !=
                MessageBoxResult.OK)
            {
                e.Cancel = true;
            }
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);

            var handler = _di.Resolve<IKeyHandler>();
            handler?.RiseKeyDown(e);
        }
    }
}
