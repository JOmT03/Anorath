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
    // One row in the customers table (Model)
    public class CustomerItem
    {
        public int CustomerId { get; set; }
        public string CustomerCode { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public string? ContactNumber { get; set; }
        public string? EmailAddress { get; set; }
        public string? Address { get; set; }
        public bool IsActive { get; set; }

        public string ContactText => string.IsNullOrWhiteSpace(ContactNumber) ? "—" : ContactNumber;
        public string EmailText => string.IsNullOrWhiteSpace(EmailAddress) ? "—" : EmailAddress;
        public string ActiveText => IsActive ? "Yes" : "No";
    }

    public sealed partial class CustomersPage : Page
    {
        private readonly ObservableCollection<CustomerItem> _customers = new();

        public CustomersPage()
        {
            InitializeComponent();
            CustomersList.ItemsSource = _customers;
            Loaded += async (s, e) => await LoadAsync();
        }

        // READ + SEARCH
        private async Task LoadAsync()
        {
            try
            {
                var search = SearchBox.Text.Trim();
                var url = string.IsNullOrEmpty(search) ? "customers" : $"customers?search={Uri.EscapeDataString(search)}";
                var list = await ApiClient.GetAsync<List<CustomerItem>>(url);

                _customers.Clear();
                foreach (var c in list) _customers.Add(c);
                EmptyText.Visibility = _customers.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
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
            if (await ShowCustomerDialogAsync(null))
            {
                Show(InfoBarSeverity.Success, "Customer added.");
                await LoadAsync();
            }
        }

        // UPDATE
        private async void Edit_Click(object sender, RoutedEventArgs e)
        {
            var customer = FindCustomer(sender);
            if (customer == null) return;

            if (await ShowCustomerDialogAsync(customer))
            {
                Show(InfoBarSeverity.Success, "Customer updated.");
                await LoadAsync();
            }
        }

        // DELETE
        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            var customer = FindCustomer(sender);
            if (customer == null) return;

            var confirm = new ContentDialog
            {
                Title = "Delete customer?",
                Content = $"Delete {customer.CustomerName} ({customer.CustomerCode})? This cannot be undone.",
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot
            };
            if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;

            try
            {
                await ApiClient.DeleteAsync($"customers/{customer.CustomerId}");
                Show(InfoBarSeverity.Success, "Customer deleted.");
                await LoadAsync();
            }
            catch (Exception ex)
            {
                Show(InfoBarSeverity.Error, ex.Message);
            }
        }

        private CustomerItem? FindCustomer(object sender) =>
            sender is Button { Tag: int id } ? _customers.FirstOrDefault(c => c.CustomerId == id) : null;

        // Add / Edit form (existing == null means Add)
        private async Task<bool> ShowCustomerDialogAsync(CustomerItem? existing)
        {
            var nameBox = new TextBox { Header = "Full name", Text = existing?.CustomerName ?? "", MaxLength = 150 };
            var contactBox = new TextBox { Header = "Contact number", Text = existing?.ContactNumber ?? "", MaxLength = 20, PlaceholderText = "09171234567" };
            var emailBox = new TextBox { Header = "Email (optional)", Text = existing?.EmailAddress ?? "", MaxLength = 150 };
            var addressBox = new TextBox { Header = "Address (optional)", Text = existing?.Address ?? "", MaxLength = 250 };
            var activeBox = new CheckBox { Content = "Active", IsChecked = existing?.IsActive ?? true };
            var errorText = new TextBlock { Foreground = new SolidColorBrush(Colors.Firebrick), TextWrapping = TextWrapping.Wrap };

            var form = new StackPanel { Spacing = 12, Width = 360 };
            if (existing != null)
                form.Children.Add(new TextBlock { Text = "Code: " + existing.CustomerCode, Opacity = 0.7 });
            form.Children.Add(nameBox);
            form.Children.Add(contactBox);
            form.Children.Add(emailBox);
            form.Children.Add(addressBox);
            form.Children.Add(activeBox);
            form.Children.Add(errorText);

            var dialog = new ContentDialog
            {
                Title = existing == null ? "Add Customer" : $"Edit {existing.CustomerName}",
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
                    if (nameBox.Text.Trim().Length < 2)
                    {
                        errorText.Text = "Full name is required.";
                        args.Cancel = true;
                        return;
                    }

                    var body = new
                    {
                        customerName = nameBox.Text.Trim(),
                        contactNumber = contactBox.Text.Trim(),
                        emailAddress = emailBox.Text.Trim(),
                        address = addressBox.Text.Trim(),
                        isActive = activeBox.IsChecked == true
                    };

                    if (existing == null)
                        await ApiClient.PostAsync("customers", body);
                    else
                        await ApiClient.PutAsync($"customers/{existing.CustomerId}", body);
                }
                catch (Exception ex)
                {
                    errorText.Text = ex.Message;   // e.g. "Email address is not valid."
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