using DeezFiles.Services;
using DeezFiles.Utilities;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace DeezFiles
{
    public partial class LoginPage : Page
    {
        public LoginPage()
        {
            InitializeComponent();
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string username = Username.Text;
                string password = Password.Password;

                string authResult = await AuthorizationService.LoginUser(username, password);

                if (string.IsNullOrEmpty(authResult))
                {
                    LoginError.Text = "Invalid username or password";
                }
                else
                {
                    AuthorizationService.accountAddress = authResult;
                    AuthorizationService.currentUsername = username;
                    AuthorizationService.currentPassword = password;

                    LocalFileHelper.EnsureInitialized(username);
                    AuthorizationService.CreateDeviceNodeAddress();
                    CryptHelper.SavePasswordDerivedMasterKey(username, password, LocalFileHelper.configPath);
                    LocalFileHelper.SaveDNETaddress(username, authResult);

                    try
                    {
                        await AuthorizationService.MakeNodeOnline();
                    }
                    catch
                    {
                        // Login can still continue; the UI will show files, but P2P needs this to download shards.
                    }

                    MessageBox.Show("Login success: " + authResult); // ✅ DEBUG

                    this.NavigationService.Navigate(new Uri("Views/MainPage.xaml", UriKind.Relative));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("ERROR: " + ex.Message);
            }
        }

        private void RegButton_Click(object sender, RoutedEventArgs e)
        {
            this.NavigationService.Navigate(new Uri("Views/RegPage.xaml", UriKind.RelativeOrAbsolute));


        }
    }
}
