using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ERP
{
    internal class SaleDto
    {
        public int SaleId { get; set; }
        public DateTime SaleDate { get; set; }
        public string ItemName { get; set; } = "";
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Total { get; set; }
        public string Guest { get; set; } = "";
        public string PaymentMethod { get; set; } = "";
    }

    // Restaurant point-of-sale: record sales of menu items (Products)
    public class RestaurantView : UserControl
    {
        private readonly ApiClient _api;
        private readonly ComboBox cboItem = new() { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly NumericUpDown numQty = new() { Minimum = 1, Maximum = 999, Value = 1 };
        private readonly ComboBox cboGuest = new() { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly ComboBox cboPayment = new() { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly Label lblTotal = new() { Font = AppTheme.SubHeadingFont, ForeColor = AppTheme.TextDark };
        private readonly Label lblToday = new() { Font = AppTheme.ButtonFont, ForeColor = AppTheme.Teal };
        private readonly DataGridView grid = UiKit.Grid();

        public RestaurantView(string companyCode)
        {
            _api = new ApiClient(companyCode);
            BackColor = AppTheme.Background;

            cboPayment.Items.AddRange(new object[] { "Cash", "Card", "Charge to Room" });
            cboPayment.SelectedIndex = 0;

            var top = new Panel();
            top.Controls.Add(UiKit.Heading("Restaurant — Point of Sale"));
            UiKit.AddField(top, "Menu Item *", cboItem, 20, 260, 45);
            UiKit.AddField(top, "Qty *", numQty, 290, 70, 45);
            UiKit.AddField(top, "Guest", cboGuest, 370, 230, 45);
            UiKit.AddField(top, "Payment", cboPayment, 610, 150, 45);

            var btnRecord = UiKit.ActionButton("✓", "Record Sale", 20, 110, 160);
            var btnDelete = UiKit.ActionButton("✕", "Void Sale", 190, 110, 140, false);
            var btnRefresh = UiKit.ActionButton("↻", "Refresh", 340, 110, 120, false);
            btnRecord.Click += async (s, e) => await RecordSaleAsync();
            btnDelete.Click += async (s, e) => await VoidSaleAsync();
            btnRefresh.Click += async (s, e) => await LoadAllAsync();
            top.Controls.AddRange(new Control[] { btnRecord, btnDelete, btnRefresh });

            lblTotal.SetBounds(480, 114, 300, 28);
            lblToday.SetBounds(20, 155, 700, 24);
            top.Controls.Add(lblTotal);
            top.Controls.Add(lblToday);

            cboItem.SelectedIndexChanged += (s, e) => UpdateTotal();
            numQty.ValueChanged += (s, e) => UpdateTotal();
            UpdateTotal();

            UiKit.StackLayout(this, top, 185, grid);
            Load += async (s, e) => await LoadAllAsync();
        }

        private async Task LoadAllAsync()
        {
            await LoadLookupsAsync();
            await LoadTodaySalesAsync();
        }

        private async Task LoadLookupsAsync()
        {
            try
            {
                var products = await _api.GetAsync<List<ProductDto>>("products");
                cboItem.Items.Clear();
                foreach (var p in products.Where(p => p.IsActive).OrderBy(p => p.ProductName))
                    cboItem.Items.Add(new OptionItem(p.ProductId, $"{p.ProductName}  ({UiKit.Peso(p.Price)})", p.Price));

                var customers = await _api.GetAsync<List<CustomerLookupDto>>("customers");
                cboGuest.Items.Clear();
                cboGuest.Items.Add(new OptionItem(0, "Walk-in (no guest)"));
                foreach (var c in customers.Where(c => c.IsActive).OrderBy(c => c.CustomerName))
                    cboGuest.Items.Add(new OptionItem(c.CustomerId, c.CustomerName));
                cboGuest.SelectedIndex = 0;
            }
            catch (Exception ex) { UiKit.ShowError(ex); }
        }

        private async Task LoadTodaySalesAsync()
        {
            try
            {
                var today = DateTime.Today.ToString("yyyy-MM-dd");
                var sales = await _api.GetAsync<List<SaleDto>>($"sales?from={today}&to={today}");
                grid.DataSource = sales;
                UiKit.HideColumns(grid, "SaleId");
                UiKit.FormatMoney(grid, "UnitPrice", "Total");
                var col = grid.Columns["SaleDate"];
                if (col != null) col.DefaultCellStyle.Format = "h:mm tt";

                lblToday.Text = $"Today's restaurant sales: {UiKit.Peso(sales.Sum(s => s.Total))}   •   {sales.Count} transaction(s)";
            }
            catch (Exception ex) { UiKit.ShowError(ex); }
        }

        private void UpdateTotal()
        {
            var price = (cboItem.SelectedItem as OptionItem)?.Extra ?? 0m;
            lblTotal.Text = "Total: " + UiKit.Peso(price * numQty.Value);
        }

        private async Task RecordSaleAsync()
        {
            if (cboItem.SelectedItem is not OptionItem item)
            {
                UiKit.Warn("Please select a menu item. (Add items in the Restaurant Menu tab first.)");
                return;
            }

            var guest = cboGuest.SelectedItem as OptionItem;
            int? customerId = guest == null || guest.Id == 0 ? null : guest.Id;
            var payment = cboPayment.SelectedItem?.ToString() ?? "Cash";

            if (payment == "Charge to Room" && customerId is null)
            {
                UiKit.Warn("Select a guest to charge this to their room.");
                return;
            }

            try
            {
                await _api.PostAsync("sales", new
                {
                    productId = item.Id,
                    customerId,
                    quantity = (int)numQty.Value,
                    paymentMethod = payment
                });
                numQty.Value = 1;
                await LoadTodaySalesAsync();
            }
            catch (Exception ex) { UiKit.ShowError(ex); }
        }

        private async Task VoidSaleAsync()
        {
            if (grid.CurrentRow?.DataBoundItem is not SaleDto sale)
            {
                UiKit.Warn("Select a sale from the list first.");
                return;
            }
            if (!UiKit.Confirm($"Void sale: {sale.Quantity} x {sale.ItemName} ({UiKit.Peso(sale.Total)})?")) return;

            try
            {
                await _api.DeleteAsync($"sales/{sale.SaleId}");
                await LoadTodaySalesAsync();
            }
            catch (Exception ex) { UiKit.ShowError(ex); }
        }
    }
}