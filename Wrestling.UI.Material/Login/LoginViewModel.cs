using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Controls;
using System.Windows.Input;
using Wrestling.Integration;
using Wrestling.Providers;
using Wrestling.UI.Material.Home;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Login
{
    public class LoginViewModel : ViewModelBase
    {
        private ICommand _loginCommand;
        private string _password;
        private string _validation;
        private string _userName;

        public LoginViewModel(IDiContainer container) : base(container)
        {
        }

        public override string PageTitle => "Логин";
        
        #region Binding Properties

        public ICommand LoginCommand
        {
            get
            {
                if (_loginCommand == null)
                {
                    _loginCommand = new RelayCommand(
                        param => Login(UserName, (param as PasswordBox)?.Password ?? string.Empty),
                        param => true
                    );
                }
                return _loginCommand;
            }
        }

        public string UserName
        {
            get { return _userName; }
            set
            {
                _userName = value;

                OnPropertyChanged("UserName");
            }
        }

        public string Password
        {
            get { return _password; }
            set
            {
                _password = value;

                OnPropertyChanged("Password");
            }
        }

        public string Validation
        {
            get { return _validation; }
            set
            {
                _validation = value;

                OnPropertyChanged("Validation");
            }
        }

        #endregion

        private void Login(string userName, string password)
        {
            if (string.IsNullOrEmpty(userName))
            {
                Validation = "Ввведите имя пользователя";
                return;
            }

            if (string.IsNullOrEmpty(password))
            {
                Validation = "Ввведите пароль";
                return;
            }

            var api = DiContainer.Resolve<IRosbosApi>();
            var cache = DiContainer.Resolve<ICacheManager>();

            if (false && !CheckPreviousAuth(userName, password))
            {
                if (api.CheckConnection())
                {
                    if (!VerifyLogin(api, userName, password))
                    {
                        Validation = "Неправильные данные";
                        return;
                    }

                    DataContext.IsAuthenticated = true;
                }
                else
                {
                    Validation = "Неправильные данные";
                    return;
                }
            }
            else
            {
                api.SetCredentials(userName, password);
                DataContext.IsAuthenticated = true;
            }

            if (api.CheckConnection())
            {
                UpdateCache(api, cache);
            }

            InitDataContextWithCache(cache);

            if (DataContext.IsAuthenticated)
            {
                NavigateToView<HomeViewModel>();
            }
            else
            {
                Validation = "Ошибка авторизации";
            }
        }

        private bool CheckPreviousAuth(string userName, string password)
        {
            if (File.Exists("Cache_User.txt"))
            {
                try
                {
                    var fileStream = new FileStream("Cache_User.txt", FileMode.Open, FileAccess.Read);
                    using (var streamReader = new StreamReader(fileStream, Encoding.UTF8))
                    {
                        var text = streamReader.ReadToEnd();

                        var hash = ComputeSha256Hash($"{userName}{password}");

                        return text == hash;
                    }
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }

        private bool VerifyLogin(IRosbosApi api, string userName, string password)
        {
            api.SetCredentials(userName, password);

            var token = api.LoadToken();

            if (!token)
            {
                return false;
            }

            var hash = ComputeSha256Hash($"{userName}{password}");

            if (!string.IsNullOrEmpty(hash))
            {
                File.WriteAllText("Cache_User.txt", hash);
            }

            return true;
        }


        static string ComputeSha256Hash(string rawData)
        {
            // Create a SHA256   
            using (SHA256 sha256Hash = SHA256.Create())
            {
                // ComputeHash - returns byte array  
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));

                // Convert byte array to a string   
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }

        private void UpdateCache(IRosbosApi api, ICacheManager cache)
        {
            var teams = api.GetTeams();
            DataContext.TeamsCache = teams;

            var wrestlers = api.GetWrestlers();
            DataContext.WrestlersCache = wrestlers;

            CheckTeamLogo();

            if (cache != null)
            {
                cache.SaveTeams(teams);
                cache.SaveWrestlers(wrestlers);
            }
        }

        private void InitDataContextWithCache(ICacheManager cache)
        {
            if (cache != null && (DataContext.WrestlersCache == null || DataContext.WrestlersCache.Count == 0 || DataContext.TeamsCache == null || DataContext.TeamsCache.Count == 0))
            {
                DataContext.WrestlersCache = cache.LoadWrestlers();
                DataContext.TeamsCache = cache.LoadTeams();
            }
        }

        private void CheckTeamLogo()
        {
            foreach (var app in DataContext.TeamsCache)
            {
                if (!string.IsNullOrEmpty(app.EmblemPath))
                {
                    // get file name and check if it exists
                    var fileNameItems = app.EmblemPath.Split('\\');
                    var fileName = fileNameItems[fileNameItems.Length - 1];

                    var storagePath = Path.GetFullPath("Images");
                    var fullPath = $"{storagePath}\\{fileName}";

                    if (!File.Exists(fullPath))
                    {
                        using (WebClient client = new WebClient())
                        {
                            client.DownloadFile($"https://rosbos.ru/{app.EmblemPath}", fullPath);
                        }
                    }

                    app.EmblemPath = fullPath;
                }
            }
        }
    } 
}