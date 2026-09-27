using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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
        public string Target { get; set; } = "";   // menu Tag to open when clicked
        public Brush Accent { get; set; } = new SolidColorBrush(ColorHelper.FromArgb(255, 0x2E, 0x61, 0x71));
    }

    // One month of trend data (GET analytics/monthly-revenue)
    public class MonthPoint
    {
        public string Month { get; set; } = "";
        public decimal Revenue { get; set; }
        public decimal RoomRevenue { get; set; }
        public decimal RestaurantRevenue { get; set; }
        public int Reservations { get; set; }
        public int Visits { get; set; }
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
                FinanceKpis.Add(Card("Total Revenue", Money(d, "totalRevenue"), "\uE9D2", Green, "Financials"));
                FinanceKpis.Add(Card("Room Revenue", Money(d, "roomRevenue"), "\uE80F", Teal, "Reservations"));
                FinanceKpis.Add(Card("Restaurant Revenue", Money(d, "restaurantRevenue"), "\uE7BF", Teal, "POS"));
                FinanceKpis.Add(Card("Total Expenses", Money(d, "totalExpenses"), "\uE8A5", Red, "SupplyChain"));
                FinanceKpis.Add(Card("Net Income", Money(d, "netIncome"), "\uE73E", Green, "Financials"));

                OperationsKpis.Clear();
                OperationsKpis.Add(Card("Total Rooms", Count(d, "totalRooms"), "\uE80F", Slate, "Rooms"));
                OperationsKpis.Add(Card("Available Rooms", Count(d, "availableRooms"), "\uE73E", Green, "Rooms"));
                OperationsKpis.Add(Card("Occupied Rooms", Count(d, "occupiedRooms"), "\uE77B", Amber, "Rooms"));
                OperationsKpis.Add(Card("Total Reservations", Count(d, "totalReservations"), "\uE787", Teal, "Reservations"));
                OperationsKpis.Add(Card("Pending Reservations", Count(d, "pendingReservations"), "\uE81C", Amber, "Reservations"));
                OperationsKpis.Add(Card("Checked-In Guests", Count(d, "checkedInReservations"), "\uE8FB", Teal, "Reservations"));
                OperationsKpis.Add(Card("Customers", Count(d, "totalCustomers"), "\uE716", Slate, "Customers"));
                OperationsKpis.Add(Card("Active Employees", Count(d, "totalEmployees"), "\uE77B", Slate, "Payroll"));
                OperationsKpis.Add(Card("Pending Purchase Orders", Count(d, "pendingPurchaseOrders"), "\uE8A5", Amber, "SupplyChain"));

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

            // Graphs load separately so a chart problem never hides the KPI cards
            try
            {
                var months = await ApiClient.GetAsync<List<MonthPoint>>("analytics/monthly-revenue?months=6");

                DrawBars(VisitsChart, Teal, months.Select(m => new Bar(
                    ShortMonth(m.Month),
                    m.Visits,
                    m.Visits.ToString("N0"),
                    $"{m.Month}\n{m.Visits:N0} guest stay(s)\n{m.Reservations:N0} booking(s) incl. pending/cancelled")).ToList());

                DrawBars(RevenueChart, Green, months.Select(m => new Bar(
                    ShortMonth(m.Month),
                    (double)m.Revenue,
                    Compact(m.Revenue),
                    $"{m.Month}\nTotal: ₱{m.Revenue:N2}\nRooms: ₱{m.RoomRevenue:N2}\nRestaurant: ₱{m.RestaurantRevenue:N2}")).ToList());
            }
            catch (Exception ex)
            {
                ShowNotice("Graphs unavailable: " + ex.Message);
            }
            finally
            {
                Spinner.IsActive = false;
            }
        }

        // ---------- Clickable KPI cards ----------
        private void Kpi_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is not KpiItem kpi || string.IsNullOrEmpty(kpi.Target)) return;

            var opened = ShellPage.Current?.NavigateTo(kpi.Target) ?? false;
            if (!opened)
                ShowNotice($"Your role or plan does not include the module for \"{kpi.Title}\".");
        }

        // ---------- Simple bar chart (no extra NuGet package) ----------
        private record Bar(string Label, double Value, string ValueText, string Tooltip);

        private static void DrawBars(Grid host, Windows.UI.Color color, List<Bar> data)
        {
            host.Children.Clear();
            host.RowDefinitions.Clear();
            host.ColumnDefinitions.Clear();

            var max = data.Count == 0 ? 0 : data.Max(b => b.Value);
            if (max <= 0)
            {
                host.Children.Add(new TextBlock
                {
                    Text = "No data yet for this period.",
                    Opacity = 0.6,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                });
                return;
            }

            host.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            host.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            const double plotHeight = 170;
            var maxIndex = data.FindIndex(b => b.Value == max);
            var lastIndex = data.Count - 1;

            for (var i = 0; i < data.Count; i++)
            {
                var b = data[i];
                host.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var height = b.Value / max * plotHeight;
                if (b.Value > 0 && height < 3) height = 3;   // tiny values stay visible

                var column = new StackPanel
                {
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Background = new SolidColorBrush(Colors.Transparent),   // bigger hover target than the bar
                    Padding = new Thickness(2, 0, 2, 0)
                };

                // Label only the highest and the latest month; hover shows every value
                if (i == maxIndex || i == lastIndex)
                    column.Children.Add(new TextBlock
                    {
                        Text = b.ValueText,
                        FontSize = 12,
                        Opacity = 0.8,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 0, 0, 4)
                    });

                column.Children.Add(new Border
                {
                    Width = 28,
                    Height = height,
                    Background = new SolidColorBrush(color),
                    CornerRadius = new CornerRadius(4, 4, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Center
                });

                ToolTipService.SetToolTip(column, b.Tooltip);
                Grid.SetColumn(column, i);
                host.Children.Add(column);

                var label = new TextBlock
                {
                    Text = b.Label,
                    FontSize = 12,
                    Opacity = 0.7,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 6, 0, 0)
                };
                Grid.SetRow(label, 1);
                Grid.SetColumn(label, i);
                host.Children.Add(label);
            }

            // Baseline
            var baseline = new Border
            {
                Height = 1,
                VerticalAlignment = VerticalAlignment.Bottom,
                Background = new SolidColorBrush(ColorHelper.FromArgb(90, 128, 128, 128))
            };
            Grid.SetColumnSpan(baseline, data.Count);
            host.Children.Add(baseline);
        }

        // "Sep 2026" -> "Sep"
        private static string ShortMonth(string month) => month.Split(' ')[0];

        // ₱45,230 -> ₱45.2K
        private static string Compact(decimal v) =>
            v >= 1_000_000 ? $"₱{v / 1_000_000:0.#}M" :
            v >= 1_000 ? $"₱{v / 1_000:0.#}K" :
            $"₱{v:N0}";

        // ---------- Helpers ----------
        private void ShowError(string message)
        {
            ErrorBar.Message = message;
            ErrorBar.IsOpen = true;
        }

        private void ShowNotice(string message)
        {
            NoticeBar.Message = message;
            NoticeBar.IsOpen = true;
        }

        private static KpiItem Card(string title, string value, string glyph, Windows.UI.Color color, string target)
            => new KpiItem { Title = title, Value = value, Glyph = glyph, Accent = new SolidColorBrush(color), Target = target };

        private static string Money(JsonElement d, string key)
            => d.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number
                ? "₱" + v.GetDecimal().ToString("N2") : "—";

        private static string Count(JsonElement d, string key)
            => d.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number
                ? v.GetInt32().ToString("N0") : "—";
    }
}