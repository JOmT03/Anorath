using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ERP
{
    internal class SupplierDto
    {
        public int SupplierId { get; set; }
        public string SupplierCode { get; set; } = "";
        public string SupplierName { get; set; } = "";
        public string? ContactNumber { get; set; }
        public string? EmailAddress { get; set; }
        public bool IsActive { get; set; }
    }

    internal class PurchaseOrderDto
    {
        public int PurchaseOrderId { get; set; }
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = "";
        public DateTime OrderDate { get; set; }
        public string ItemDescription { get; set; } = "";
        public int Quantity { get; set; }
        public decimal UnitCost { get; set; }
        public decimal TotalCost { get; set; }
        public string Status { get; set; } = "";
        public DateTime? ReceivedDate { get; set; }
    }

    public class SupplyChainView : UserControl
    {
        private readonly ApiClient _api;

        // Suppliers
        private readonly TextBox txtCode = new(), txtName = new(), txtContact = new(), txtEmail = new();
        private readonly CheckBox chkActive = new() { Text = "Active", Checked = true };
        private readonly DataGridView gridSuppliers = UiKit.Grid();
        private int? _selectedSupplierId;

        // Purchase orders
        private readonly ComboBox cboSupplier = new() { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly DateTimePicker dtpOrderDate = new() { Format = DateTimePickerFormat.Short };
        private readonly TextBox txtItem = new();
        private readonly NumericUpDown numQty = new() { Minimum = 1, Maximum = 100000, Value = 1 };
        private readonly NumericUpDown numUnitCost = new() { Minimum = 0, Maximum = 10000000, DecimalPlaces = 2, ThousandsSeparator = true };
        private readonly Label lblPoTotal = new() { Font = AppTheme.SubHeadingFont, ForeColor = AppTheme.TextDark };
        private readonly DataGridView gridOrders = UiKit.Grid();

        public SupplyChainView(string companyCode)
        {
            _api = new ApiClient(companyCode);
            BackColor = AppTheme.Background;

            var tabs = new TabControl { Dock = DockStyle.Fill, Font = AppTheme.BodyFont };
            tabs.TabPages.Add(BuildSuppliersPage());
            tabs.TabPages.Add(BuildOrdersPage());
            Controls.Add(tabs);

            Load += async (s, e) =>
            {
                await LoadSuppliersAsync();
                await LoadOrdersAsync();
            };
        }

        // ---------------- SUPPLIERS ----------------
        private TabPage BuildSuppliersPage()
        {
            var page = new TabPage("Suppliers") { BackColor = AppTheme.Background };
            var top = new Panel();

            UiKit.AddField(top, "Supplier Code *", txtCode, 20, 160);
            UiKit.AddField(top, "Supplier Name *", txtName, 190, 220);
            UiKit.AddField(top, "Contact Number", txtContact, 420, 160);
            UiKit.AddField(top, "Email", txtEmail, 590, 230);

            chkActive.SetBounds(20, 82, 90, 24);
            chkActive.Font = AppTheme.BodyFont;
            top.Controls.Add(chkActive);

            var btnAdd = UiKit.ActionButton("+", "Add", 120, 76);
            var btnUpdate = UiKit.ActionButton("✎", "Update", 260, 76);
            var btnDelete = UiKit.ActionButton("✕", "Delete", 400, 76, 130, false);
            var btnClear = UiKit.ActionButton("⟲", "Clear", 540, 76, 130, false);
            btnAdd.Click += async (s, e) => await SaveSupplierAsync(isNew: true);
            btnUpdate.Click += async (s, e) => await SaveSupplierAsync(isNew: false);
            btnDelete.Click += async (s, e) => await DeleteSupplierAsync();
            btnClear.Click += (s, e) => ClearSupplier();
            top.Controls.AddRange(new Control[] { btnAdd, btnUpdate, btnDelete, btnClear });

            gridSuppliers.CellClick += (s, e) =>
            {
                if (e.RowIndex >= 0 && gridSuppliers.Rows[e.RowIndex].DataBoundItem is SupplierDto d) FillSupplier(d);
            };

            UiKit.StackLayout(page, top, 125, gridSuppliers);
            return page;
        }

        private async Task LoadSuppliersAsync()
        {
            try
            {
                var suppliers = await _api.GetAsync<List<SupplierDto>>("suppliers");
                gridSuppliers.DataSource = suppliers;
                UiKit.HideColumns(gridSuppliers, "SupplierId");

                var previous = (cboSupplier.SelectedItem as OptionItem)?.Id;
                cboSupplier.Items.Clear();
                foreach (var s in suppliers.Where(s => s.IsActive))
                    cboSupplier.Items.Add(new OptionItem(s.SupplierId, $"{s.SupplierName} ({s.SupplierCode})"));
                foreach (OptionItem item in cboSupplier.Items)
                    if (item.Id == previous) cboSupplier.SelectedItem = item;
            }
            catch (Exception ex) { UiKit.ShowError(ex); }
        }

        private void FillSupplier(SupplierDto d)
        {
            _selectedSupplierId = d.SupplierId;
            txtCode.Text = d.SupplierCode;
            txtName.Text = d.SupplierName;
            txtContact.Text = d.ContactNumber ?? "";
            txtEmail.Text = d.EmailAddress ?? "";
            chkActive.Checked = d.IsActive;
        }

        private void ClearSupplier()
        {
            _selectedSupplierId = null;
            txtCode.Clear();
            txtName.Clear();
            txtContact.Clear();
            txtEmail.Clear();
            chkActive.Checked = true;
            gridSuppliers.ClearSelection();
        }

        private async Task SaveSupplierAsync(bool isNew)
        {
            if (string.IsNullOrWhiteSpace(txtCode.Text) || string.IsNullOrWhiteSpace(txtName.Text))
            {
                UiKit.Warn("Supplier Code and Supplier Name are required.");
                return;
            }
            if (!string.IsNullOrWhiteSpace(txtEmail.Text) && !txtEmail.Text.Contains('@'))
            {
                UiKit.Warn("Please enter a valid email address.");
                return;
            }
            if (!isNew && _selectedSupplierId is null)
            {
                UiKit.Warn("Select a supplier from the list first.");
                return;
            }

            var body = new
            {
                supplierCode = txtCode.Text.Trim(),
                supplierName = txtName.Text.Trim(),
                contactNumber = UiKit.NullIfEmpty(txtContact.Text),
                emailAddress = UiKit.NullIfEmpty(txtEmail.Text),
                isActive = chkActive.Checked
            };

            try
            {
                if (isNew) await _api.PostAsync("suppliers", body);
                else await _api.PutAsync($"suppliers/{_selectedSupplierId}", body);
                ClearSupplier();
                await LoadSuppliersAsync();
            }
            catch (Exception ex) { UiKit.ShowError(ex); }
        }

        private async Task DeleteSupplierAsync()
        {
            if (_selectedSupplierId is null)
            {
                UiKit.Warn("Select a supplier from the list first.");
                return;
            }
            if (!UiKit.Confirm($"Delete supplier \"{txtName.Text}\"?")) return;

            try
            {
                await _api.DeleteAsync($"suppliers/{_selectedSupplierId}");
                ClearSupplier();
                await LoadSuppliersAsync();
            }
            catch (Exception ex) { UiKit.ShowError(ex); }
        }

        // ---------------- PURCHASE ORDERS ----------------
        private TabPage BuildOrdersPage()
        {
            var page = new TabPage("Purchase Orders") { BackColor = AppTheme.Background };
            var top = new Panel();

            UiKit.AddField(top, "Supplier *", cboSupplier, 20, 220);
            UiKit.AddField(top, "Order Date", dtpOrderDate, 250, 130);
            UiKit.AddField(top, "Item / Description *", txtItem, 390, 220);
            UiKit.AddField(top, "Qty *", numQty, 620, 70);
            UiKit.AddField(top, "Unit Cost *", numUnitCost, 700, 120);

            var btnCreate = UiKit.ActionButton("+", "Create PO", 20, 76, 150);
            var btnReceived = UiKit.ActionButton("✓", "Mark Received", 180, 76, 170);
            var btnCancel = UiKit.ActionButton("⊘", "Cancel PO", 360, 76, 140, false);
            var btnDelete = UiKit.ActionButton("✕", "Delete", 510, 76, 120, false);
            btnCreate.Click += async (s, e) => await CreateOrderAsync();
            btnReceived.Click += async (s, e) => await SetOrderStatusAsync("Received");
            btnCancel.Click += async (s, e) => await SetOrderStatusAsync("Cancelled");
            btnDelete.Click += async (s, e) => await DeleteOrderAsync();
            top.Controls.AddRange(new Control[] { btnCreate, btnReceived, btnCancel, btnDelete });

            lblPoTotal.SetBounds(645, 82, 200, 26);
            top.Controls.Add(lblPoTotal);
            numQty.ValueChanged += (s, e) => UpdatePoTotal();
            numUnitCost.ValueChanged += (s, e) => UpdatePoTotal();
            UpdatePoTotal();

            UiKit.ColorStatus(gridOrders);
            UiKit.StackLayout(page, top, 125, gridOrders);
            return page;
        }

        private void UpdatePoTotal() => lblPoTotal.Text = "Total: " + UiKit.Peso(numQty.Value * numUnitCost.Value);

        private PurchaseOrderDto? SelectedOrder => gridOrders.CurrentRow?.DataBoundItem as PurchaseOrderDto;

        private async Task LoadOrdersAsync()
        {
            try
            {
                var orders = await _api.GetAsync<List<PurchaseOrderDto>>("purchaseorders");
                gridOrders.DataSource = orders;
                UiKit.HideColumns(gridOrders, "SupplierId");
                UiKit.FormatMoney(gridOrders, "UnitCost", "TotalCost");
                UiKit.FormatDate(gridOrders, "OrderDate", "ReceivedDate");
            }
            catch (Exception ex) { UiKit.ShowError(ex); }
        }

        private async Task CreateOrderAsync()
        {
            if (cboSupplier.SelectedItem is not OptionItem supplier)
            {
                UiKit.Warn("Please select a supplier. (Add one in the Suppliers tab first.)");
                return;
            }
            if (string.IsNullOrWhiteSpace(txtItem.Text))
            {
                UiKit.Warn("Item / Description is required.");
                return;
            }

            var body = new
            {
                supplierId = supplier.Id,
                orderDate = dtpOrderDate.Value.Date,
                itemDescription = txtItem.Text.Trim(),
                quantity = (int)numQty.Value,
                unitCost = numUnitCost.Value
            };

            try
            {
                await _api.PostAsync("purchaseorders", body);
                txtItem.Clear();
                numQty.Value = 1;
                numUnitCost.Value = 0;
                await LoadOrdersAsync();
            }
            catch (Exception ex) { UiKit.ShowError(ex); }
        }

        private async Task SetOrderStatusAsync(string status)
        {
            var order = SelectedOrder;
            if (order is null)
            {
                UiKit.Warn("Select a purchase order from the list first.");
                return;
            }
            if (order.Status == status) return;
            if (!UiKit.Confirm($"Mark PO #{order.PurchaseOrderId} ({order.ItemDescription}) as {status}?")) return;

            try
            {
                await _api.PutAsync($"purchaseorders/{order.PurchaseOrderId}/status", new { status });
                await LoadOrdersAsync();
            }
            catch (Exception ex) { UiKit.ShowError(ex); }
        }

        private async Task DeleteOrderAsync()
        {
            var order = SelectedOrder;
            if (order is null)
            {
                UiKit.Warn("Select a purchase order from the list first.");
                return;
            }
            if (!UiKit.Confirm($"Delete PO #{order.PurchaseOrderId}?")) return;

            try
            {
                await _api.DeleteAsync($"purchaseorders/{order.PurchaseOrderId}");
                await LoadOrdersAsync();
            }
            catch (Exception ex) { UiKit.ShowError(ex); }
        }
    }
}