using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ERP
{
    public class DashboardView : UserControl
    {
        private readonly ApiClient _api;
        private readonly Dictionary<string, Label> _values = new();
        private readonly Label _lblStatus;

        private static readonly (string Caption, string Key, bool Money, string Accent)[] Cards =
        {
            ("Total Revenue", "totalRevenue", true, "green"),
            ("Total Expenses", "totalExpenses", true, "red"),
            ("Net Income", "netIncome", true, "teal"),
            ("Total Rooms", "totalRooms", false, "slate"),
            ("Available Rooms", "availableRooms", false, "green"),
            ("Occupied Rooms", "occupiedRooms", false, "amber"),
            ("Total Reservations", "totalReservations", false, "teal"),
            ("Pending Reservations", "pendingReservations", false, "amber"),
            ("Checked-In Guests", "checkedInReservations", false, "teal"),
            ("Customers", "totalCustomers", false, "slate"),
            ("Active Employees", "totalEmployees", false, "slate"),
            ("Pending Purchase Orders", "pendingPurchaseOrders", false, "amber"),
        };

        public DashboardView(string companyCode)
        {
            _api = new ApiClient(companyCode);
            BackColor = AppTheme.Background;

            var header = new Panel();
            header.Controls.Add(UiKit.Heading("Dashboard"));
            var btnRefresh = UiKit.ActionButton("↻", "Refresh", 20, 48, 120);
            btnRefresh.Click += async (s, e) => await LoadAsync();
            header.Controls.Add(btnRefresh);
            _lblStatus = new Label { Left = 155, Top = 56, Width = 400, Height = 20, ForeColor = AppTheme.TextMuted, Font = AppTheme.BodyFont };
            header.Controls.Add(_lblStatus);

            var flow = new FlowLayoutPanel { AutoScroll = true, WrapContents = true, Padding = new Padding(0, 5, 0, 0) };
            foreach (var card in Cards)
            {
                flow.Controls.Add(UiKit.KpiCard(card.Caption, AccentFor(card.Accent), out var valueLabel));
                _values[card.Key] = valueLabel;
            }

            UiKit.StackLayout(this, header, 95, flow);
            Load += async (s, e) => await LoadAsync();
        }

        private static Color AccentFor(string name) => name switch
        {
            "green" => UiKit.Green,
            "red" => UiKit.Red,
            "amber" => UiKit.Amber,
            "slate" => UiKit.Slate,
            _ => AppTheme.Teal
        };

        private async Task LoadAsync()
        {
            try
            {
                _lblStatus.Text = "Loading...";
                var data = await _api.GetAsync<JsonElement>("dashboard/summary");

                foreach (var card in Cards)
                {
                    if (!data.TryGetProperty(card.Key, out var value)) continue;
                    var label = _values[card.Key];
                    if (card.Money)
                    {
                        var amount = value.GetDecimal();
                        label.Text = UiKit.Peso(amount);
                        if (card.Key == "netIncome") label.ForeColor = amount < 0 ? UiKit.Red : UiKit.Green;
                    }
                    else
                    {
                        label.Text = value.GetInt32().ToString("N0");
                    }
                }

                _lblStatus.Text = "Updated " + DateTime.Now.ToString("h:mm tt");
            }
            catch (Exception ex)
            {
                _lblStatus.Text = "";
                UiKit.ShowError(ex);
            }
        }
    }
}