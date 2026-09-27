using System.Net.Http;
using System.Threading.Tasks;
using Anorath.WinUI.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Anorath.WinUI.Pages
{
    public sealed partial class LoginPage : Page
    {
        // Shape of the JSON returned by POST auth/login
        private class LoginResponse
        {
            public string Token { get; set; } = "";
            public int UserId { get; set; }
            public string Username { get; set; } = "";
            public string FullName { get; set; } = "";
            public string Role { get; set; } = "";
            public string? CompanyCode { get; set; }
            public string? CompanyName { get; set; }
            public string Plan { get; set; } = "";
        }

        private bool _busy;

        public LoginPage()
        {
            InitializeComponent();
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e) => await TryLoginAsync();

        private async void PasswordInput_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter) await TryLoginAsync();
        }

        private async Task TryLoginAsync()
        {
            if (_busy) return;               // ignore double clicks
            MessageBar.IsOpen = false;

            // Client-side validation
            if (string.IsNullOrWhiteSpace(UsernameBox.Text) || string.IsNullOrWhiteSpace(PasswordInput.Password))
            {
                ShowMessage(InfoBarSeverity.Warning, "Username and Password are required.");
                return;
            }

            _busy = true;
            try
            {
                Session.Clear();             // no company header on the login call

                var result = await ApiClient.PostAsync<LoginResponse>("auth/login", new
                {
                    username = UsernameBox.Text.Trim(),
                    password = PasswordInput.Password
                });

                // Master tells us who the user is and which company (tenant DB) to use
                Session.UserId = result.UserId;
                Session.Token = result.Token;
                Session.Username = result.Username;
                Session.FullName = result.FullName;
                Session.Role = result.Role;
                Session.CompanyCode = result.CompanyCode ?? "";
                Session.CompanyName = result.CompanyName ?? "";
                Session.Plan = result.Plan;

                PasswordInput.Password = "";
                MainWindow.Current?.NavigateTo(typeof(ShellPage));
            }
            catch (ApiException ex)
            {
                // 401 wrong password, 403 suspended / expired
                ShowMessage(InfoBarSeverity.Error, ex.Message);
            }
            catch (HttpRequestException)
            {
                ShowMessage(InfoBarSeverity.Error, "Cannot reach the server. Make sure Anorath.api is running.");
            }
            finally
            {
                _busy = false;
            }
        }

        private void ShowMessage(InfoBarSeverity severity, string message)
        {
            MessageBar.Severity = severity;
            MessageBar.Message = message;
            MessageBar.IsOpen = true;
        }
    }
}