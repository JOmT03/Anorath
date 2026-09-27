using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Anorath.WinUI.Services;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace Anorath.WinUI.Pages
{
    // One row in the users table (Model)
    public class UserItem
    {
        public int Id { get; set; }
        public string Username { get; set; } = "";
        public string FullName { get; set; } = "";
        public string Role { get; set; } = "";
        public bool IsActive { get; set; }

        public string StatusText => IsActive ? "Active" : "Inactive";
        public bool CanEdit => Role != "Admin";   // Admin accounts are protected
    }

    public sealed partial class UsersPage : Page
    {
        private readonly ObservableCollection<UserItem> _users = new();

        public UsersPage()
        {
            InitializeComponent();
            UsersList.ItemsSource = _users;
            Loaded += async (s, e) => await LoadAsync();
        }

        // READ + SEARCH
        private async Task LoadAsync()
        {
            try
            {
                var search = SearchBox.Text.Trim();
                var url = string.IsNullOrEmpty(search) ? "users" : $"users?search={Uri.EscapeDataString(search)}";

                var list = await ApiClient.GetAsync<List<UserItem>>(url);
                _users.Clear();
                foreach (var u in list) _users.Add(u);

                EmptyText.Visibility = _users.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                Show(InfoBarSeverity.Error, ex.Message);
            }
        }

        private async void Search_Click(object sender, RoutedEventArgs e) => await LoadAsync();

        private async void ShowAll_Click(object sender, RoutedEventArgs e)
        {
            SearchBox.Text = "";
            await LoadAsync();
        }

        private async void SearchBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter) await LoadAsync();
        }

        // CREATE
        private async void Add_Click(object sender, RoutedEventArgs e)
        {
            if (await ShowUserDialogAsync(null))
            {
                Show(InfoBarSeverity.Success, "User created.");
                await LoadAsync();
            }
        }

        // UPDATE
        private async void Edit_Click(object sender, RoutedEventArgs e)
        {
            var user = FindUser(sender);
            if (user == null) return;

            if (await ShowUserDialogAsync(user))
            {
                Show(InfoBarSeverity.Success, "User updated.");
                await LoadAsync();
            }
        }

        // DELETE (with confirmation)
        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            var user = FindUser(sender);
            if (user == null) return;

            var confirm = new ContentDialog
            {
                Title = "Delete user?",
                Content = $"Delete '{user.Username}' ({user.Role})? This cannot be undone.",
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot
            };
            if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;

            try
            {
                await ApiClient.DeleteAsync($"users/{user.Id}");
                Show(InfoBarSeverity.Success, "User deleted.");
                await LoadAsync();
            }
            catch (Exception ex)
            {
                Show(InfoBarSeverity.Error, ex.Message);
            }
        }

        private UserItem? FindUser(object sender) =>
            sender is Button { Tag: int id } ? _users.FirstOrDefault(u => u.Id == id) : null;

        // Add / Edit form (existing == null means Add)
        private async Task<bool> ShowUserDialogAsync(UserItem? existing)
        {
            List<string> roles;
            try
            {
                roles = await ApiClient.GetAsync<List<string>>("users/roles");
            }
            catch (Exception ex)
            {
                Show(InfoBarSeverity.Error, ex.Message);
                return false;
            }

            var usernameBox = new TextBox { Header = "Username", Text = existing?.Username ?? "", IsEnabled = existing == null };
            var fullNameBox = new TextBox { Header = "Full name", Text = existing?.FullName ?? "" };
            var roleBox = new ComboBox { Header = "Role", ItemsSource = roles, HorizontalAlignment = HorizontalAlignment.Stretch };
            roleBox.SelectedItem = existing != null && roles.Contains(existing.Role) ? existing.Role : roles.FirstOrDefault();
            var passwordBox = new PasswordBox
            {
                Header = existing == null ? "Password (min 8, letters and numbers)" : "New password (leave blank to keep)"
            };
            var activeBox = new CheckBox
            {
                Content = "Active",
                IsChecked = existing?.IsActive ?? true,
                Visibility = existing == null ? Visibility.Collapsed : Visibility.Visible
            };
            var errorText = new TextBlock { Foreground = new SolidColorBrush(Colors.Firebrick), TextWrapping = TextWrapping.Wrap };

            var form = new StackPanel { Spacing = 12, Width = 360 };
            form.Children.Add(usernameBox);
            form.Children.Add(fullNameBox);
            form.Children.Add(roleBox);
            form.Children.Add(passwordBox);
            form.Children.Add(activeBox);
            form.Children.Add(errorText);

            var dialog = new ContentDialog
            {
                Title = existing == null ? "Add User" : $"Edit {existing.Username}",
                Content = form,
                PrimaryButtonText = "Save",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot
            };

            dialog.PrimaryButtonClick += async (s, args) =>
            {
                var deferral = args.GetDeferral();
                try
                {
                    // Client-side validation (the API checks again)
                    if (string.IsNullOrWhiteSpace(usernameBox.Text) || string.IsNullOrWhiteSpace(fullNameBox.Text) || roleBox.SelectedItem == null)
                    {
                        errorText.Text = "Username, full name and role are required.";
                        args.Cancel = true;
                        return;
                    }
                    if (existing == null && passwordBox.Password.Length < 8)
                    {
                        errorText.Text = "Password must be at least 8 characters.";
                        args.Cancel = true;
                        return;
                    }

                    if (existing == null)
                    {
                        await ApiClient.PostAsync("users", new
                        {
                            username = usernameBox.Text.Trim(),
                            fullName = fullNameBox.Text.Trim(),
                            password = passwordBox.Password,
                            role = roleBox.SelectedItem.ToString()
                        });
                    }
                    else
                    {
                        await ApiClient.PutAsync($"users/{existing.Id}", new
                        {
                            fullName = fullNameBox.Text.Trim(),
                            role = roleBox.SelectedItem.ToString(),
                            isActive = activeBox.IsChecked == true,
                            password = string.IsNullOrEmpty(passwordBox.Password) ? null : passwordBox.Password
                        });
                    }
                }
                catch (Exception ex)
                {
                    errorText.Text = ex.Message;   // e.g. "Username is already taken."
                    args.Cancel = true;
                }
                finally
                {
                    deferral.Complete();
                }
            };

            return await dialog.ShowAsync() == ContentDialogResult.Primary;
        }

        private void Show(InfoBarSeverity severity, string message)
        {
            MessageBar.Severity = severity;
            MessageBar.Message = message;
            MessageBar.IsOpen = true;
        }
    }
}