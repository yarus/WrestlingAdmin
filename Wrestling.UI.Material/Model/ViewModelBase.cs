using System.Collections.Generic;
using System.Windows.Input;
using MvvmDialogs;
using Wrestling.Entities;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Model
{
    public abstract class ViewModelBase : ObservableObject
    {
        private ICommand _backCommand;

        private GlobalSettings _settings;
        private IDataContext _dataContext;
        private INavigationService _navService;
        private IDialogService _dialogService;

        private readonly IDiContainer _container;

        public virtual IList<CommandButtonItem> DrawerItems { get; }
        public virtual IList<CommandButtonItem> QuickButtons { get; }

        public virtual string WindowTitle
        {
            get
            {
                if (DataContext.Tournament == null)
                {
                    return "Вольная борьба - Администратор турниров";
                }

                if (string.IsNullOrEmpty(DataContext.Tournament.FileName))
                {
                    return DataContext.Tournament.Name;
                }

                return DataContext.Tournament.FileName + " - " + DataContext.Tournament.Name;
            }
        }
        public virtual string PageTitle => string.Empty;

        public virtual bool IsBackButtonAvailable => false;

        protected IDiContainer DiContainer => _container;

        protected GlobalSettings GlobalSettings => _settings;

        protected IDataContext DataContext => _dataContext;

        protected IDialogService Dialog => _dialogService;

        protected ViewModelBase(IDiContainer container)
        {
            _container = container;

            DrawerItems = new List<CommandButtonItem>();
            QuickButtons = new List<CommandButtonItem>();
        }

        public virtual void InitData()
        {
            _dialogService = Resolve<IDialogService>();
            _dataContext = Resolve<IDataContext>();
            _navService = Resolve<INavigationService>();
            _settings = Resolve<GlobalSettings>();
        }
        
        public virtual void OnNavigationCompleted()
        {
            
        }

        public ICommand BackCommand
        {
            get
            {
                if (_backCommand == null)
                {
                    _backCommand = new RelayCommand(
                        param => OnBackCommand(),
                        param => true
                    );
                }
                return _backCommand;
            }
        }

        protected virtual void OnNavigatingOut()
        {

        }

        protected void NavigateToView<T>() where T : ViewModelBase
        {
            OnNavigatingOut();

            _navService.NavigateToView<T>();
        }

        protected void ShowPrintPreview(ViewModelBase vm)
        {
            _navService.ShowPrintPreview(vm);
        }

        protected T Resolve<T>(string key) where T : class
        {
            return _container.Resolve(key) as T;
        }

        protected T Resolve<T>() where T : class
        {
            return _container.Resolve<T>();
        }

        protected void CloseApp()
        {
            OnNavigatingOut();

            _navService.CloseApp();
        }

        protected void ShowSnackMessage(string message)
        {
            _navService.ShellVm.ShowSnackbarMessage(message);
        }
        
        protected virtual void OnBackCommand()
        {
            
        }
    }
}