using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
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

            Task.Factory.StartNew(() =>
            {
                Thread.Sleep(2500);
            }).ContinueWith(t =>
            {
                //note you can use the message queue from any thread, but just for the demo here we 
                //need to get the message queue from the snackbar, so need to be on the dispatcher
                MainSnackbar.MessageQueue.Enqueue("Добро пожаловать в Администратор турниров по вольной борьбе!");
            }, TaskScheduler.FromCurrentSynchronizationContext());

            var vm = new MainWindowViewModel(MainSnackbar.MessageQueue, _di);
            vm.OnRequestClose += ViewModelRequestClose;
            vm.InitData();

            DataContext = vm;
        }

        private void ViewModelRequestClose(object sender, System.EventArgs e)
        {
            Close();
        }

        private void UIElement_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            //until we had a StaysOpen glag to Drawer, this will help with scroll bars
            var dependencyObject = Mouse.Captured as DependencyObject;
            while (dependencyObject != null)
            {
                if (dependencyObject is ScrollBar) return;
                dependencyObject = VisualTreeHelper.GetParent(dependencyObject);
            }

            MenuToggleButton.IsChecked = false;
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
