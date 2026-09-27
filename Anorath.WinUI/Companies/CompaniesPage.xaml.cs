using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Anorath.WinUI.Services;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace Anorath.WinUI.Pages
{
    public class CompanyRow
    {
        public int CompanyId { get; set; }
        public string CompanyCode { get; set; } = "";
        public string CompanyName { get; set; } = "";
        public string Plan { get; set; } = "";
        public bool IsActive { get; set; }
        public DateTime? SubscriptionEnd { get; set; }
        public int? DaysLeft { get; set; }
        public string ServerName { get; set; } = "";
        public string DatabaseName { get; set; } = "";
        public string AuthMode { get; set; } = "";
        public int UserCount { get; set; }

        public string DbText => string.IsNullOrEmpty(AuthMode) ? DatabaseName : $"{DatabaseName} · {AuthMode}";
        public string StatusText => IsActive ? "Active" : "Suspended";
        public string ToggleText => IsActive ? "Suspend company" : "Activate company";
        public bool IsExpired => DaysLeft is < 0;
        public bool IsExpiringSoon => DaysLeft is >= 0 and <= 30;

        public string SubscriptionText =>
            SubscriptionEnd == null ? "No end date"
            : IsExpired ? $"Expired {SubscriptionEnd:MMM dd, yyyy}"
            : $"Until {SubscriptionEnd:MMM dd, yyyy} · {DaysLeft} day(s) left";

        public SolidColorBrush StatusBrush => new(IsActive ? Colors.SeaGreen : Colors.Firebrick);
        public SolidColorBrush SubscriptionBrush => new(IsExpired ? Colors.Firebrick : IsExpiringSoon ? Colors.DarkOrange : Colors.Gray);
    }

    public class ConnectionResult
    {
        public bool Ok { get; set; }
        public string Server { get; set; } = "";
        public string Database { get; set; } = "";
        public string ServerVersion { get; set; } = "";
        public long ElapsedMs { get; set; }
        public string Message { get; set; } = "";
    }

    public sealed partial class CompaniesPage : Page
    {
        private static readonly string[] Plans = { "Micro", "Small", "Medium" };
        private readonly ObservableCollection<CompanyRow> _companies = new();

        public CompaniesPage()
        {
            InitializeComponent();
            CompaniesList.ItemsSource = _companies;
            Loaded += async (s, e) => await LoadAsync();
        }

        private async Task LoadAsync()
        {
            try
            {
                var search = SearchBox.Text.Trim();
                var url = string.IsNullOrEmpty(search) ? "companies" : $"companies?search={Uri.EscapeDataString(search)}";
                var list = await ApiClient.GetAsync<List<CompanyRow>>(url);

                _companies.Clear();
                foreach (var c in list) _companies.Add(c);
                EmptyText.Visibility = _companies.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                RenderSummary(list);
            }
            catch (Exception ex)
            {
                Show(InfoBarSeverity.Error, ex.Message);
            }
        }

        private void RenderSummary(List<CompanyRow> list)
        {
            SummaryPanel.Children.Clear();
            AddCard("Tenant companies", list.Count.ToString(), Colors.SteelBlue);
            AddCard("Active", list.Count(c => c.IsActive).ToString(), Colors.SeaGreen);
            AddCard("Suspended", list.Count(c => !c.IsActive).ToString(), Colors.Firebrick);
            AddCard("Expiring ≤ 30 days", list.Count(c => c.IsExpiringSoon).ToString(), Colors.DarkOrange);
        }

        private void AddCard(string label, string value, Windows.UI.Color accent)
        {
            var panel = new StackPanel { Spacing = 4 };
            panel.Children.Add(new TextBlock { Text = label, Opacity = 0.7 });
            panel.Children.Add(new TextBlock { Text = value, FontSize = 26, FontWeight = FontWeights.SemiBold });
            SummaryPanel.Children.Add(new Border
            {
                Width = 190,
                Padding = new Thickness(16, 12, 16, 12),
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(4, 1, 1, 1),
                BorderBrush = new SolidColorBrush(accent),
                Child = panel
            });
        }

        private async void Search_Click(object sender, RoutedEventArgs e) => await LoadAsync();
        private async void Refresh_Click(object sender, RoutedEventArgs e) { SearchBox.Text = ""; await LoadAsync(); }

        private async void SearchBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter) await LoadAsync();
        }

        private CompanyRow? Find(object sender) =>
            sender is FrameworkElement { Tag: int id } ? _companies.FirstOrDefault(c => c.CompanyId == id) : null;

        // ---------- Test connection to the tenant's own server ----------
        private async void TestConnection_Click(object sender, RoutedEventArgs e)
        {
            var c = Find(sender);
            if (c == null) return;

            Show(InfoBarSeverity.Informational, $"Connecting to {c.ServerName} ...");
            try
            {
                var r = await ApiClient.GetAsync<ConnectionResult>($"companies/{c.CompanyId}/test-connection");
                MessageBar.IsOpen = false;

                var body = new StackPanel { Spacing = 6, Width = 420 };
                body.Children.Add(new TextBlock
                {
                    Text = r.Ok ? "✅ Connected" : "❌ Cannot connect",
                    FontSize = 18,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(r.Ok ? Colors.SeaGreen : Colors.Firebrick)
                });
                body.Children.Add(new TextBlock { Text = $"Company: {c.CompanyCode} · {c.CompanyName} ({c.Plan})" });
                body.Children.Add(new TextBlock { Text = $"Server: {r.Server}" });
                body.Children.Add(new TextBlock { Text = $"Database: {r.Database}" });
                body.Children.Add(new TextBlock { Text = $"Authentication: {c.AuthMode}" });
                if (r.Ok) body.Children.Add(new TextBlock { Text = $"SQL Server version: {r.ServerVersion}" });
                body.Children.Add(new TextBlock { Text = $"Response time: {r.ElapsedMs} ms", Opacity = 0.7 });
                if (!r.Ok) body.Children.Add(new TextBlock { Text = r.Message, TextWrapping = TextWrapping.Wrap, Opacity = 0.8 });

                await new ContentDialog
                {
                    Title = "Tenant server connection",
                    Content = body,
                    CloseButtonText = "Close",
                    XamlRoot = XamlRoot
                }.ShowAsync();
            }
            catch (Exception ex)
            {
                Show(InfoBarSeverity.Error, ex.Message);
            }
        }

        // ---------- Edit name / plan ----------
        private async void Edit_Click(object sender, RoutedEventArgs e)
        {
            var c = Find(sender);
            if (c == null) return;

            var nameBox = new TextBox { Header = "Company name", Text = c.CompanyName, MaxLength = 150 };
            var planBox = new ComboBox { Header = "Subscription plan", ItemsSource = Plans, SelectedItem = Plans.Contains(c.Plan) ? c.Plan : "Micro", HorizontalAlignment = HorizontalAlignment.Stretch };
            var note = new TextBlock
            {
                Text = "Micro: rooms, reservations, restaurant, reports.\nSmall: + supply chain, payroll, financial statements, dashboard.\nMedium: + branches.\nUsers see the new modules the next time they sign in.",
                Opacity = 0.7,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap
            };
            var errorText = new TextBlock { Foreground = new SolidColorBrush(Colors.Firebrick), TextWrapping = TextWrapping.Wrap };

            var form = new StackPanel { Spacing = 10, Width = 360 };
            form.Children.Add(new TextBlock { Text = c.CompanyCode, Opacity = 0.7 });
            form.Children.Add(nameBox);
            form.Children.Add(planBox);
            form.Children.Add(note);
            form.Children.Add(errorText);

            var dialog = new ContentDialog
            {
                Title = "Edit company",
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
                    if (nameBox.Text.Trim().Length < 2)
                    { errorText.Text = "Company name is required."; args.Cancel = true; return; }

                    await ApiClient.PutAsync($"companies/{c.CompanyId}", new { companyName = nameBox.Text.Trim(), plan = planBox.SelectedItem as string ?? "Micro" });
                }
                catch (Exception ex) { errorText.Text = ex.Message; args.Cancel = true; }
                finally { deferral.Complete(); }
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                Show(InfoBarSeverity.Success, $"{c.CompanyCode} updated.");
                await LoadAsync();
            }
        }

        // ---------- Extend subscription ----------
        private async void Extend_Click(object sender, RoutedEventArgs e)
        {
            var c = Find(sender);
            if (c == null) return;

            var options = new[] { "1 month", "3 months", "6 months", "12 months" };
            var months = new[] { 1, 3, 6, 12 };
            var box = new ComboBox { Header = "Extend by", ItemsSource = options, SelectedIndex = 3, HorizontalAlignment = HorizontalAlignment.Stretch };
            var preview = new TextBlock { FontWeight = FontWeights.SemiBold };

            void UpdatePreview()
            {
                var baseDate = c.SubscriptionEnd.HasValue && c.SubscriptionEnd.Value > DateTime.UtcNow ? c.SubscriptionEnd.Value : DateTime.UtcNow;
                preview.Text = $"New end date: {baseDate.AddMonths(months[Math.Max(0, box.SelectedIndex)]):MMMM dd, yyyy}";
            }
            box.SelectionChanged += (s, a) => UpdatePreview();
            UpdatePreview();

            var form = new StackPanel { Spacing = 10, Width = 340 };
            form.Children.Add(new TextBlock { Text = $"{c.CompanyCode} · {c.CompanyName}\nCurrent: {c.SubscriptionText}", Opacity = 0.8 });
            form.Children.Add(box);
            form.Children.Add(preview);

            var dialog = new ContentDialog
            {
                Title = "Extend subscription",
                Content = form,
                PrimaryButtonText = "Extend",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

            try
            {
                await ApiClient.PostAsync($"companies/{c.CompanyId}/extend", new { months = months[Math.Max(0, box.SelectedIndex)] });
                Show(InfoBarSeverity.Success, $"Subscription of {c.CompanyCode} extended.");
                await LoadAsync();
            }
            catch (Exception ex)
            {
                Show(InfoBarSeverity.Error, ex.Message);
            }
        }

        // ---------- Suspend / Activate ----------
        private async void ToggleStatus_Click(object sender, RoutedEventArgs e)
        {
            var c = Find(sender);
            if (c == null) return;

            var suspend = c.IsActive;
            var dialog = new ContentDialog
            {
                Title = suspend ? "Suspend company?" : "Activate company?",
                Content = suspend
                    ? $"All {c.UserCount} user(s) of {c.CompanyName} will be blocked from signing in until the company is activated again. Their data is kept."
                    : $"Users of {c.CompanyName} will be able to sign in again.",
                PrimaryButtonText = suspend ? "Suspend" : "Activate",
                CloseButtonText = "Back",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

            try
            {
                await ApiClient.PutAsync($"companies/{c.CompanyId}/status", new { isActive = !suspend });
                Show(InfoBarSeverity.Success, $"{c.CompanyCode} is now {(suspend ? "suspended" : "active")}.");
                await LoadAsync();
            }
            catch (Exception ex)
            {
                Show(InfoBarSeverity.Error, ex.Message);
            }
        }

        // ---------- Helpers ----------
        private void Row_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Grid g && g.Children.OfType<Button>().LastOrDefault() is Button b) b.Opacity = 1;
        }

        private void Row_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Grid g && g.Children.OfType<Button>().LastOrDefault() is Button b) b.Opacity = 0.3;
        }

        private void Show(InfoBarSeverity severity, string message)
        {
            MessageBar.Severity = severity;
            MessageBar.Message = message;
            MessageBar.IsOpen = true;
        }
    }
}