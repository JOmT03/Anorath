using System;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Threading.Tasks;
using Anorath.WinUI.Services;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Anorath.WinUI.Pages
{
    // One card on the dashboard
    public class KpiItem
    {
        public string Title { get; set; } = "";
        public string Value { get; set; } = "";
        public string Glyph { get; set; } = "";
        public Brush Accent { get; set; } = new SolidColorBrush(ColorHelper.FromArgb(255, 0x2E, 0x61, 0x71));
    }

    public sealed partial class DashboardPage : Page
    {
        private static readonly Windows.UI.Color Green = ColorHelper.FromArgb(255, 0x2E, 0x8B, 0x57);
        private static readonly Windows.UI.Color Red = ColorHelper.FromArgb(255, 0xB9, 0x4A, 0x48);
        private static readonly Windows.UI.Color Teal = ColorHelper.FromArgb(255, 0x2E, 0x61, 0x71);
        private static readonly Windows.UI.Color Amber = ColorHelper.FromArgb(255, 0xD0, 0x8C, 0x2E);
        private static readonly Windows.UI.Color Slate = ColorHelper.FromArgb(255, 0x5A, 0x6F, 0x7A);

        public ObservableCollection<KpiItem> FinanceKpis { get; } = new();
        public ObservableCollection<KpiItem> OperationsKpis { get; } = new();

        public DashboardPage()
        {
            InitializeComponent();
            Loaded += async (s, e) => await LoadAsync();
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e) => await LoadAsync();

        private async Task LoadAsync()
        {
            Spinner.IsActive = true;
            ErrorBar.IsOpen = false;
            try
            {
                var d = await ApiClient.GetAsync<JsonElement>("dashboard/summary");

                FinanceKpis.Clear();
                FinanceKpis.Add(Card("Total Revenue", Money(d, "totalRevenue"), "\uE9D2", Green));
                FinanceKpis.Add(Card("Room Revenue", Money(d, "roomRevenue"), "\uE80F", Teal));
                FinanceKpis.Add(Card("Restaurant Revenue", Money(d, "restaurantRevenue"), "\uE7BF", Teal));
                FinanceKpis.Add(Card("Total Expenses", Money(d, "totalExpenses"), "\uE8A5", Red));
                FinanceKpis.Add(Card("Net Income", Money(d, "netIncome"), "\uE73E", Green));

                OperationsKpis.Clear();
                OperationsKpis.Add(Card("Total Rooms", Count(d, "totalRooms"), "\uE80F", Slate));
                OperationsKpis.Add(Card("Available Rooms", Count(d, "availableRooms"), "\uE73E", Green));
                OperationsKpis.Add(Card("Occupied Rooms", Count(d, "occupiedRooms"), "\uE77B", Amber));
                OperationsKpis.Add(Card("Total Reservations", Count(d, "totalReservations"), "\uE787", Teal));
                OperationsKpis.Add(Card("Pending Reservations", Count(d, "pendingReservations"), "\uE81C", Amber));
                OperationsKpis.Add(Card("Checked-In Guests", Count(d, "checkedInReservations"), "\uE8FB", Teal));
                OperationsKpis.Add(Card("Customers", Count(d, "totalCustomers"), "\uE716", Slate));
                OperationsKpis.Add(Card("Active Employees", Count(d, "totalEmployees"), "\uE77B", Slate));
                OperationsKpis.Add(Card("Pending Purchase Orders", Count(d, "pendingPurchaseOrders"), "\uE8A5", Amber));

                StatusText.Text = "Updated " + DateTime.Now.ToString("h:mm tt");
            }
            catch (ApiException ex)
            {
                ShowError(ex.Message);
            }
            catch (Exception)
            {
                ShowError("Cannot reach the server. Make sure Anorath.api is running.");
            }
            finally
            {
                Spinner.IsActive = false;
            }
        }

        private void ShowError(string message)
        {
            ErrorBar.Message = message;
            ErrorBar.IsOpen = true;
        }

        private static KpiItem Card(string title, string value, string glyph, Windows.UI.Color color)
            => new KpiItem { Title = title, Value = value, Glyph = glyph, Accent = new SolidColorBrush(color) };

        private static string Money(JsonElement d, string key)
            => d.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number
                ? "₱" + v.GetDecimal().ToString("N2") : "—";

        private static string Count(JsonElement d, string key)
            => d.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number
                ? v.GetInt32().ToString("N0") : "—";
    }
}