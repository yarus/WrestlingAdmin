using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;
using MvvmDialogs.FrameworkDialogs.OpenFile;
using Wrestling.Entities;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Tournament.Standing.Applications
{
    public class AddAppViewModel : ViewModelBase
    {
        private TeamApplication _item;
        private TeamApplication _selectedItem;

        private ObservableCollection<TeamApplication> _cachedTeams;
        private Dictionary<string, string> _registeredTeams;

        private ICommand _setEmblemCommand;

        public AddAppViewModel(IDiContainer container, TeamApplication app) : base(container)
        {
            _item = app;
        }

        public override void InitData()
        {
            base.InitData();

            CachedTeams = new ObservableCollection<TeamApplication>(DataContext.TeamsCache);

            _registeredTeams = DataContext.Tournament.TeamApplications.Where(x => !string.IsNullOrEmpty(x.HashTag)).ToDictionary(x => x.HashTag, y => y.ShortName);
        }

        public TeamApplication Item
        {
            get { return _item; }
            set
            {
                _item = value;
                OnPropertyChanged("Item");
            }
        }
        
        public TeamApplication SelectedItem
        {
            get { return _selectedItem; }
            set
            {
                _selectedItem = value;

                if (_selectedItem != null)
                {
                    _item.Sync(_selectedItem);

                    _selectedItem = null;

                    OnPropertyChanged("Item");
                }

                OnPropertyChanged("SelectedItem");
            }
        }

        public ICommand SetEmblemCommand
        {
            get
            {
                if (_setEmblemCommand == null)
                {
                    _setEmblemCommand = new RelayCommand(
                        param => SetEmblem(),
                        param => true
                    );
                }
                return _setEmblemCommand;
            }
        }

        public AutoCompleteFilterPredicate<object> TeamFilter
        {
            get
            {
                return (searchText, obj) =>
                {
                    var item = obj as TeamApplication;

                    if (item == null || string.IsNullOrEmpty(searchText) || searchText.Length < 3) return false;

                    if (!string.IsNullOrEmpty(item.HashTag) && _registeredTeams.ContainsKey(item.HashTag)) return false;
                    
                    return (!string.IsNullOrEmpty(item.HashTag) && item.HashTag.ToLower().Contains(searchText.ToLower()))
                            || (!string.IsNullOrEmpty(item.ShortName) && item.ShortName.ToLower().Contains(searchText.ToLower()))
                            || (!string.IsNullOrEmpty(item.FullName) && item.FullName.ToLower().Contains(searchText.ToLower()))
                            || (!string.IsNullOrEmpty(item.City) && item.City.ToLower().Contains(searchText.ToLower()))
                            || (!string.IsNullOrEmpty(item.FullAddress) && item.FullAddress.ToLower().Contains(searchText.ToLower()))
                            || (!string.IsNullOrEmpty(item.MainCoach) && item.MainCoach.ToLower().Contains(searchText.ToLower()));
                };
            }
        }

        public ObservableCollection<TeamApplication> CachedTeams
        {
            get { return _cachedTeams; }
            set
            {
                _cachedTeams = value;

                OnPropertyChanged("CachedTeams");
            }
        }

        private void SetEmblem()
        {
            var settings = new OpenFileDialogSettings
            {
                Title = "Открыть файл с изображением",
                InitialDirectory = string.IsNullOrEmpty(Item.EmblemPath) ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) : Item.EmblemPath,
                Filter = "Изображение (*.png)|*.png|Изображение (*.jpeg)|*.jpeg|Изображение (*.bmp)|*.bmp|Изображение (*.gif)|*.gif"
            };

            bool? success = Dialog.ShowOpenFileDialog(this, settings);
            if (success == true)
            {
                // Copy to Images folder
                var fileNameItems = settings.FileName.Split('\\');
                var fileName = fileNameItems[fileNameItems.Length - 1];
                var ext = Path.GetExtension(settings.FileName);

                var storagePath = Path.GetFullPath("Images");
                var filePattern = $"{(string.IsNullOrEmpty(Item.HashTag) ? Item.ShortName : Item.HashTag)}{ext}";
                var fullPath = $"{storagePath}\\{filePattern}";

                var previousPath = Item.EmblemPath;

                try
                {
                    File.Copy(settings.FileName, fullPath, true);
                    Item.EmblemPath = filePattern;
                }
                catch(Exception ex)
                {
                    ShowSnackMessage($"При сохранении изображения произошла ошибка: {ex.Message}");
                    Item.EmblemPath = previousPath;
                }                
            }
        }
    }
}
