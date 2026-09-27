using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Anorath.WinUI.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Anorath.WinUI.Pages
{
    // One recorded sale (Model)
    public class SaleItem
    {
        public int SaleId { get; set; }
        public DateTime SaleDate { get; set; }
        public string ItemName { get; set; } = "";
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Total { get; set; }
        public string Guest { get; set; } = "";
        public string PaymentMethod { get; set; } = "";

        public string TimeText => SaleDate.ToString("h:mm tt");
        public string TotalText => "₱" + Total.ToString("N2");
        public bool CanVoid => Session.Role == "Admin" || Session.Role == "Manager";
    }

    // A checked-in guest who can charge to their room
    public class GuestOption
    {
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = "";
        public string RoomNumber { get; set; } = "";
    }

    public sealed partial class PosPage : Page
    {
        private static readonly string[] PaymentMethods = { "Cash", "Card", "Charge to Room" };

        private List<ProductItem> _products = new();
        private List<GuestOption> _guests = new();
        private readonly ObservableCollection<SaleItem> _sales = new();

        public PosPage()
        {
            InitializeComponent();
            SalesList.ItemsSource = _sales;
            PaymentBox.ItemsSource = PaymentMethods;
            PaymentBox.SelectedIndex = 0;
            Loaded += async (s, e) => await ReloadAllAsync();
        }

        private async Task ReloadAllAsync()
        {
            await LoadItemsAsync();
            await LoadGuestsAsync();
            await LoadSalesAsync();
        }

        // Menu items that can be sold (active + in stock)
        private async Task LoadItemsAsync()
        {
            try
            {
                var selectedId = ItemBox.SelectedIndex >= 0 && ItemBox.SelectedIndex < _products.Count
                    ? _products[ItemBox.SelectedIndex].ProductId : 0;

                _products = (await ApiClient.GetAsync<List<ProductItem>>("products"))
                    .Where(p => p.IsActive && p.StockQuantity > 0).ToList();

                ItemBox.ItemsSource = _products.Select(p => $"{p.ProductName} · {p.PriceText} · {p.StockQuantity} left").ToList();
                var index = _products.FindIndex(p => p.ProductId == selectedId);
                ItemBox.SelectedIndex = _products.Count == 0 ? -1 : Math.Max(0, index);
                UpdateTotal();
            }
            catch (Exception ex)
            {
                Show(InfoBarSeverity.Error, ex.Message);
            }
        }

        private async Task LoadGuestsAsync()
        {
            try
            {
                _guests = await ApiClient.GetAsync<List<GuestOption>>("sales/guests");
                GuestBox.ItemsSource = _guests.Select(g => $"{g.CustomerName} · Room {g.RoomNumber}").ToList();
                GuestBox.SelectedIndex = _guests.Count > 0 ? 0 : -1;
            }
            catch (Exception ex)
            {
                Show(InfoBarSeverity.Error, ex.Message);
            }
        }

        private async Task LoadSalesAsync()
        {
            try
            {
                var today = DateTime.Today.ToString("yyyy-MM-dd");
                var list = await ApiClient.GetAsync<List<SaleItem>>($"sales?from={today}&to={today}");

                _sales.Clear();
                foreach (var s in list) _sales.Add(s);

                EmptyText.Visibility = _sales.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                TodayTotalText.Text = $"{_sales.Count} sale(s) · ₱{_sales.Sum(s => s.Total):N2}";
            }
            catch (Exception ex)
            {
                Show(InfoBarSeverity.Error, ex.Message);
            }
        }

        private void UpdateTotal()
        {
            // Skip while the page is still loading (controls not created yet)
            if (TotalText is null || ItemBox is null || QtyBox is null || _products is null) return;

            if (ItemBox.SelectedIndex >= 0 && ItemBox.SelectedIndex < _products.Count)
            {
                var qty = double.IsNaN(QtyBox.Value) ? 0 : QtyBox.Value;
                var total = _products[ItemBox.SelectedIndex].Price * (decimal)qty;
                TotalText.Text = $"Total: ₱{total:N2}";
            }
            else
            {
                TotalText.Text = "Total: ₱0.00";
            }
        }
        private void Inputs_Changed(object sender, SelectionChangedEventArgs e) => UpdateTotal();

        private void QtyBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) => UpdateTotal();

        // Guest list is only used for "Charge to Room"
        private void PaymentBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            GuestBox.IsEnabled = PaymentBox.SelectedItem as string == "Charge to Room";
        }

        // CREATE a sale
        private async void Record_Click(object sender, RoutedEventArgs e)
        {
            MessageBar.IsOpen = false;

            // Client-side validation (the API checks again: stock, price, guest)
            if (ItemBox.SelectedIndex < 0 || ItemBox.SelectedIndex >= _products.Count)
            {
                Show(InfoBarSeverity.Warning, "Please choose a menu item.");
                return;
            }
            if (double.IsNaN(QtyBox.Value) || QtyBox.Value < 1)
            {
                Show(InfoBarSeverity.Warning, "Quantity must be at least 1.");
                return;
            }

            var payment = PaymentBox.SelectedItem as string ?? "Cash";
            int? customerId = null;
            if (payment == "Charge to Room")
            {
                if (GuestBox.SelectedIndex < 0 || GuestBox.SelectedIndex >= _guests.Count)
                {
                    Show(InfoBarSeverity.Warning, "No checked-in guest selected. Check in a guest first to charge to their room.");
                    return;
                }
                customerId = _guests[GuestBox.SelectedIndex].CustomerId;
            }

            var item = _products[ItemBox.SelectedIndex];
            try
            {
                await ApiClient.PostAsync("sales", new
                {
                    productId = item.ProductId,
                    customerId,
                    quantity = (int)QtyBox.Value,
                    paymentMethod = payment
                });

                Show(InfoBarSeverity.Success, $"Sold {(int)QtyBox.Value} × {item.ProductName}.");
                QtyBox.Value = 1;
                await LoadItemsAsync();   // stock changed
                await LoadSalesAsync();
            }
            catch (Exception ex)
            {
                Show(InfoBarSeverity.Error, ex.Message);   // e.g. "Only 2 left of Adobo."
            }
        }

        // VOID a sale (Admin/Manager)
        private async void Void_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: int id }) return;
            var sale = _sales.FirstOrDefault(s => s.SaleId == id);
            if (sale == null) return;

            var confirm = new ContentDialog
            {
                Title = "Void sale?",
                Content = $"Void {sale.Quantity} × {sale.ItemName} ({sale.TotalText})? The stock will be returned.",
                PrimaryButtonText = "Void",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot
            };
            if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;

            try
            {
                await ApiClient.DeleteAsync($"sales/{id}");
                Show(InfoBarSeverity.Success, "Sale voided.");
                await LoadItemsAsync();
                await LoadSalesAsync();
            }
            catch (Exception ex)
            {
                Show(InfoBarSeverity.Error, ex.Message);
            }
        }

        private void Show(InfoBarSeverity severity, string message)
        {
            MessageBar.Severity = severity;
            MessageBar.Message = message;
            MessageBar.IsOpen = true;
        }
    }
}