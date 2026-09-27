using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ERP
{
    public partial class Form1 : Form
    {
        private readonly HttpClient _http = new HttpClient { BaseAddress = new Uri("http://localhost:5166/") };
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        private TextBox txtCompanyCode = null!;
        private TabControl tabControl = null!;

        // Products tab controls
        private TextBox txtProductCode = null!, txtProductName = null!, txtPrice = null!;
        private CheckBox chkProductActive = null!;
        private DataGridView dgvProducts = null!;
        private Label lblProductSelectedId = null!;

        // Rooms tab controls
        private TextBox txtRoomNumber = null!, txtRoomType = null!, txtRate = null!;
        private ComboBox cboRoomStatus = null!;
        private CheckBox chkRoomActive = null!;
        private DataGridView dgvRooms = null!;
        private Label lblRoomSelectedId = null!;

        // Reservations tab controls
        private ComboBox cboResCustomer = null!, cboResRoom = null!, cboResStatus = null!;
        private DateTimePicker dtpReservationDate = null!, dtpCheckIn = null!, dtpCheckOut = null!;
        private NumericUpDown numGuests = null!;
        private TextBox txtRemarks = null!;
        private DataGridView dgvReservations = null!;
        private Label lblReservationSelectedId = null!;

        // Customers tab controls
        private TextBox txtCustomerCode = null!, txtCustomerName = null!, txtContactNumber = null!, txtEmailAddress = null!, txtAddress = null!;
        private CheckBox chkCustomerActive = null!;
        private DataGridView dgvCustomers = null!;
        private Label lblCustomerSelectedId = null!;

        private readonly string _role = "";
        private readonly string _loggedInFullName = "";

        public Form1(string companyCode, string username, string fullName, string role)
        {
            InitializeComponent();
            _role = role;
            _loggedInFullName = fullName;
            BuildUi(companyCode);
        }

        // ============================================================
        // UI BUILD
        // ============================================================
        private void BuildUi(string companyCode)
        {
            this.Text = $"Anorath Resort ERP \u2014 {_loggedInFullName} ({_role})";
            this.Width = 1100;
            this.Height = 720;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = AppTheme.Background;

            // Header bar
            var header = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = AppTheme.DarkTeal };
            var lblTitle = new Label
            {
                Text = "Anorath Resort ERP",
                Font = AppTheme.SubHeadingFont,
                ForeColor = System.Drawing.Color.White,
                Left = 20,
                Top = 13,
                AutoSize = true
            };
            var lblUser = new Label
            {
                Text = $"{_loggedInFullName} ({_role})   \u2022   {companyCode}",
                Font = AppTheme.BodyFont,
                ForeColor = AppTheme.LightTeal,
                Dock = DockStyle.Right,
                Width = 450,
                TextAlign = System.Drawing.ContentAlignment.MiddleRight,
                Padding = new Padding(0, 0, 20, 0)
            };
            header.Controls.Add(lblTitle);
            header.Controls.Add(lblUser);

            // Hidden: NewRequest() reads the company code from this textbox
            txtCompanyCode = new TextBox { Text = companyCode, ReadOnly = true, Visible = false };

            tabControl = new TabControl { Dock = DockStyle.Fill, Font = AppTheme.BodyFont };

            // New modules
            AddModuleTab("Dashboard", new DashboardView(companyCode));

            // Existing modules
            var tabRooms = new TabPage("Rooms");
            var tabReservations = new TabPage("Reservations");
            var tabCustomers = new TabPage("Customers");
            var tabProducts = new TabPage("Restaurant Menu");
            BuildRoomsTab(tabRooms);
            BuildReservationsTab(tabReservations);
            BuildCustomersTab(tabCustomers);
            BuildProductsTab(tabProducts);
            tabControl.TabPages.Add(tabRooms);
            tabControl.TabPages.Add(tabReservations);
            tabControl.TabPages.Add(tabCustomers);
            tabControl.TabPages.Add(tabProducts);

            // More modules
            AddModuleTab("Restaurant POS", new RestaurantView(companyCode));
            AddModuleTab("Supply Chain", new SupplyChainView(companyCode));
            AddModuleTab("Payroll", new PayrollView(companyCode));

            // Auto-load data when a tab is opened (no need to click "Load")
            tabControl.SelectedIndexChanged += async (s, e) =>
            {
                var page = tabControl.SelectedTab;
                if (page == tabRooms) await LoadRoomsAsync();
                else if (page == tabReservations) await LoadReservationsTabDataAsync();
                else if (page == tabCustomers) await LoadCustomersAsync();
                else if (page == tabProducts) await LoadProductsAsync();
            };

            this.Controls.Add(tabControl);
            this.Controls.Add(header);
            this.Controls.Add(txtCompanyCode);
            tabControl.BringToFront();
        }

        private void AddModuleTab(string title, Control view)
        {
            var page = new TabPage(title) { BackColor = AppTheme.Background };
            view.Dock = DockStyle.Fill;
            page.Controls.Add(view);
            tabControl.TabPages.Add(page);
        }
        private void Form1_Load(object sender, EventArgs e)
        {
            // intentionally empty
        }

        private HttpRequestMessage NewRequest(HttpMethod method, string url)
        {
            var request = new HttpRequestMessage(method, url);
            request.Headers.Add("X-Company-Code", txtCompanyCode.Text.Trim());
            return request;
        }

        // ============================================================
        // PRODUCTS TAB
        // ============================================================
        private void BuildProductsTab(TabPage tab)
        {
            var lblProductCode = new Label { Text = "Product Code:", Left = 20, Top = 20, Width = 100 };
            txtProductCode = new TextBox { Left = 130, Top = 18, Width = 150 };

            var lblProductName = new Label { Text = "Product Name:", Left = 300, Top = 20, Width = 100 };
            txtProductName = new TextBox { Left = 400, Top = 18, Width = 200 };

            var lblPrice = new Label { Text = "Price:", Left = 20, Top = 55, Width = 100 };
            txtPrice = new TextBox { Left = 130, Top = 53, Width = 100 };

            chkProductActive = new CheckBox { Text = "Active", Left = 300, Top = 55, Width = 80, Checked = true };

            lblProductSelectedId = new Label { Text = "Selected ProductId: (none)", Left = 20, Top = 85, Width = 300 };

            var btnLoad = new Button { Text = "Load Products", Left = 20, Top = 115, Width = 120 };
            var btnAdd = new Button { Text = "Add", Left = 150, Top = 115, Width = 80 };
            var btnUpdate = new Button { Text = "Update Selected", Left = 240, Top = 115, Width = 130 };
            var btnDelete = new Button { Text = "Delete Selected", Left = 380, Top = 115, Width = 130 };
            var btnClear = new Button { Text = "Clear Form", Left = 520, Top = 115, Width = 100 };

            dgvProducts = new DataGridView
            {
                Left = 20,
                Top = 150,
                Width = 780,
                Height = 350,
                ReadOnly = true,
                AllowUserToAddRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };

            btnLoad.Click += async (s, e) => await LoadProductsAsync();
            btnAdd.Click += async (s, e) => await AddProductAsync();
            btnUpdate.Click += async (s, e) => await UpdateProductAsync();
            btnDelete.Click += async (s, e) => await DeleteProductAsync();
            btnClear.Click += (s, e) => ClearProductInputs();
            dgvProducts.SelectionChanged += DgvProducts_SelectionChanged;

            tab.Controls.Add(lblProductCode);
            tab.Controls.Add(txtProductCode);
            tab.Controls.Add(lblProductName);
            tab.Controls.Add(txtProductName);
            tab.Controls.Add(lblPrice);
            tab.Controls.Add(txtPrice);
            tab.Controls.Add(chkProductActive);
            tab.Controls.Add(lblProductSelectedId);
            tab.Controls.Add(btnLoad);
            tab.Controls.Add(btnAdd);
            tab.Controls.Add(btnUpdate);
            tab.Controls.Add(btnDelete);
            tab.Controls.Add(btnClear);
            tab.Controls.Add(dgvProducts);
        }

        private async Task LoadProductsAsync()
        {
            try
            {
                var request = NewRequest(HttpMethod.Get, "products");
                var response = await _http.SendAsync(request);
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                dgvProducts.DataSource = JsonSerializer.Deserialize<List<ProductDto>>(json, JsonOptions);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to load products: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task AddProductAsync()
        {
            if (string.IsNullOrWhiteSpace(txtProductCode.Text) || string.IsNullOrWhiteSpace(txtProductName.Text))
            {
                MessageBox.Show("Product Code and Product Name are required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!decimal.TryParse(txtPrice.Text, out var price))
            {
                MessageBox.Show("Price must be a valid number.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var newProduct = new { productCode = txtProductCode.Text.Trim(), productName = txtProductName.Text.Trim(), price, isActive = chkProductActive.Checked };
            try
            {
                var request = NewRequest(HttpMethod.Post, "products");
                request.Content = new StringContent(JsonSerializer.Serialize(newProduct), Encoding.UTF8, "application/json");
                var response = await _http.SendAsync(request);
                response.EnsureSuccessStatusCode();
                MessageBox.Show("Product added.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ClearProductInputs();
                await LoadProductsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to add product: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task UpdateProductAsync()
        {
            if (dgvProducts.CurrentRow == null)
            {
                MessageBox.Show("Select a product first.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(txtProductCode.Text) || string.IsNullOrWhiteSpace(txtProductName.Text))
            {
                MessageBox.Show("Product Code and Product Name are required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!decimal.TryParse(txtPrice.Text, out var price))
            {
                MessageBox.Show("Price must be a valid number.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var id = (int)dgvProducts.CurrentRow.Cells["ProductId"].Value;
            var updated = new { productCode = txtProductCode.Text.Trim(), productName = txtProductName.Text.Trim(), price, isActive = chkProductActive.Checked };
            try
            {
                var request = NewRequest(HttpMethod.Put, $"products/{id}");
                request.Content = new StringContent(JsonSerializer.Serialize(updated), Encoding.UTF8, "application/json");
                var response = await _http.SendAsync(request);
                response.EnsureSuccessStatusCode();
                MessageBox.Show("Product updated.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ClearProductInputs();
                await LoadProductsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to update product: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task DeleteProductAsync()
        {
            if (dgvProducts.CurrentRow == null)
            {
                MessageBox.Show("Select a product first.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            var id = (int)dgvProducts.CurrentRow.Cells["ProductId"].Value;
            if (MessageBox.Show("Delete this product?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            try
            {
                var request = NewRequest(HttpMethod.Delete, $"products/{id}");
                var response = await _http.SendAsync(request);
                response.EnsureSuccessStatusCode();
                MessageBox.Show("Product deleted.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ClearProductInputs();
                await LoadProductsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to delete product: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DgvProducts_SelectionChanged(object? sender, EventArgs e)
        {
            if (dgvProducts.CurrentRow == null) return;
            var row = dgvProducts.CurrentRow;
            txtProductCode.Text = row.Cells["ProductCode"].Value?.ToString() ?? "";
            txtProductName.Text = row.Cells["ProductName"].Value?.ToString() ?? "";
            txtPrice.Text = row.Cells["Price"].Value?.ToString() ?? "";
            chkProductActive.Checked = row.Cells["IsActive"].Value is bool b && b;
            lblProductSelectedId.Text = $"Selected ProductId: {row.Cells["ProductId"].Value}";
        }

        private void ClearProductInputs()
        {
            txtProductCode.Clear(); txtProductName.Clear(); txtPrice.Clear();
            chkProductActive.Checked = true;
            lblProductSelectedId.Text = "Selected ProductId: (none)";
        }

        // ============================================================
        // ROOMS TAB
        // ============================================================
        private void BuildRoomsTab(TabPage tab)
        {
            var lblRoomNumber = new Label { Text = "Room Number:", Left = 20, Top = 20, Width = 100 };
            txtRoomNumber = new TextBox { Left = 130, Top = 18, Width = 100 };

            var lblRoomType = new Label { Text = "Room Type:", Left = 260, Top = 20, Width = 90 };
            txtRoomType = new TextBox { Left = 360, Top = 18, Width = 150 };

            var lblRate = new Label { Text = "Rate:", Left = 20, Top = 55, Width = 100 };
            txtRate = new TextBox { Left = 130, Top = 53, Width = 100 };

            var lblStatus = new Label { Text = "Status:", Left = 260, Top = 55, Width = 90 };
            cboRoomStatus = new ComboBox { Left = 360, Top = 53, Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
            cboRoomStatus.Items.AddRange(new[] { "Available", "Occupied", "Maintenance" });
            cboRoomStatus.SelectedIndex = 0;

            chkRoomActive = new CheckBox { Text = "Active", Left = 540, Top = 55, Width = 80, Checked = true };

            lblRoomSelectedId = new Label { Text = "Selected RoomId: (none)", Left = 20, Top = 85, Width = 300 };

            var btnLoad = new Button { Text = "Load Rooms", Left = 20, Top = 115, Width = 120 };
            var btnAdd = new Button { Text = "Add", Left = 150, Top = 115, Width = 80 };
            var btnUpdate = new Button { Text = "Update Selected", Left = 240, Top = 115, Width = 130 };
            var btnDelete = new Button { Text = "Delete Selected", Left = 380, Top = 115, Width = 130 };
            var btnClear = new Button { Text = "Clear Form", Left = 520, Top = 115, Width = 100 };

            dgvRooms = new DataGridView
            {
                Left = 20,
                Top = 150,
                Width = 780,
                Height = 350,
                ReadOnly = true,
                AllowUserToAddRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };

            btnLoad.Click += async (s, e) => await LoadRoomsAsync();
            btnAdd.Click += async (s, e) => await AddRoomAsync();
            btnUpdate.Click += async (s, e) => await UpdateRoomAsync();
            btnDelete.Click += async (s, e) => await DeleteRoomAsync();
            btnClear.Click += (s, e) => ClearRoomInputs();
            dgvRooms.SelectionChanged += DgvRooms_SelectionChanged;

            tab.Controls.Add(lblRoomNumber); tab.Controls.Add(txtRoomNumber);
            tab.Controls.Add(lblRoomType); tab.Controls.Add(txtRoomType);
            tab.Controls.Add(lblRate); tab.Controls.Add(txtRate);
            tab.Controls.Add(lblStatus); tab.Controls.Add(cboRoomStatus);
            tab.Controls.Add(chkRoomActive);
            tab.Controls.Add(lblRoomSelectedId);
            tab.Controls.Add(btnLoad); tab.Controls.Add(btnAdd); tab.Controls.Add(btnUpdate);
            tab.Controls.Add(btnDelete); tab.Controls.Add(btnClear);
            tab.Controls.Add(dgvRooms);
        }

        private async Task LoadRoomsAsync()
        {
            try
            {
                var request = NewRequest(HttpMethod.Get, "rooms");
                var response = await _http.SendAsync(request);
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                dgvRooms.DataSource = JsonSerializer.Deserialize<List<RoomDto>>(json, JsonOptions);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to load rooms: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task AddRoomAsync()
        {
            if (string.IsNullOrWhiteSpace(txtRoomNumber.Text) || string.IsNullOrWhiteSpace(txtRoomType.Text))
            {
                MessageBox.Show("Room Number and Room Type are required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!decimal.TryParse(txtRate.Text, out var rate))
            {
                MessageBox.Show("Rate must be a valid number.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var newRoom = new
            {
                roomNumber = txtRoomNumber.Text.Trim(),
                roomType = txtRoomType.Text.Trim(),
                rate,
                status = cboRoomStatus.SelectedItem?.ToString() ?? "Available",
                isActive = chkRoomActive.Checked
            };

            try
            {
                var request = NewRequest(HttpMethod.Post, "rooms");
                request.Content = new StringContent(JsonSerializer.Serialize(newRoom), Encoding.UTF8, "application/json");
                var response = await _http.SendAsync(request);
                response.EnsureSuccessStatusCode();
                MessageBox.Show("Room added.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ClearRoomInputs();
                await LoadRoomsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to add room: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task UpdateRoomAsync()
        {
            if (dgvRooms.CurrentRow == null)
            {
                MessageBox.Show("Select a room first.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(txtRoomNumber.Text) || string.IsNullOrWhiteSpace(txtRoomType.Text))
            {
                MessageBox.Show("Room Number and Room Type are required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!decimal.TryParse(txtRate.Text, out var rate))
            {
                MessageBox.Show("Rate must be a valid number.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var id = (int)dgvRooms.CurrentRow.Cells["RoomId"].Value;
            var updated = new
            {
                roomNumber = txtRoomNumber.Text.Trim(),
                roomType = txtRoomType.Text.Trim(),
                rate,
                status = cboRoomStatus.SelectedItem?.ToString() ?? "Available",
                isActive = chkRoomActive.Checked
            };

            try
            {
                var request = NewRequest(HttpMethod.Put, $"rooms/{id}");
                request.Content = new StringContent(JsonSerializer.Serialize(updated), Encoding.UTF8, "application/json");
                var response = await _http.SendAsync(request);
                response.EnsureSuccessStatusCode();
                MessageBox.Show("Room updated.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ClearRoomInputs();
                await LoadRoomsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to update room: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task DeleteRoomAsync()
        {
            if (dgvRooms.CurrentRow == null)
            {
                MessageBox.Show("Select a room first.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            var id = (int)dgvRooms.CurrentRow.Cells["RoomId"].Value;
            if (MessageBox.Show("Delete this room?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            try
            {
                var request = NewRequest(HttpMethod.Delete, $"rooms/{id}");
                var response = await _http.SendAsync(request);
                response.EnsureSuccessStatusCode();
                MessageBox.Show("Room deleted.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ClearRoomInputs();
                await LoadRoomsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to delete room: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DgvRooms_SelectionChanged(object? sender, EventArgs e)
        {
            if (dgvRooms.CurrentRow == null) return;
            var row = dgvRooms.CurrentRow;
            txtRoomNumber.Text = row.Cells["RoomNumber"].Value?.ToString() ?? "";
            txtRoomType.Text = row.Cells["RoomType"].Value?.ToString() ?? "";
            txtRate.Text = row.Cells["Rate"].Value?.ToString() ?? "";
            cboRoomStatus.SelectedItem = row.Cells["Status"].Value?.ToString() ?? "Available";
            chkRoomActive.Checked = row.Cells["IsActive"].Value is bool b && b;
            lblRoomSelectedId.Text = $"Selected RoomId: {row.Cells["RoomId"].Value}";
        }

        private void ClearRoomInputs()
        {
            txtRoomNumber.Clear(); txtRoomType.Clear(); txtRate.Clear();
            cboRoomStatus.SelectedIndex = 0;
            chkRoomActive.Checked = true;
            lblRoomSelectedId.Text = "Selected RoomId: (none)";
        }

        // ============================================================
        // RESERVATIONS TAB
        // ============================================================
        private void BuildReservationsTab(TabPage tab)
        {
            var lblCustomer = new Label { Text = "Customer:", Left = 20, Top = 20, Width = 90 };
            cboResCustomer = new ComboBox { Left = 120, Top = 18, Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };

            var lblRoom = new Label { Text = "Room:", Left = 360, Top = 20, Width = 60 };
            cboResRoom = new ComboBox { Left = 430, Top = 18, Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };

            var lblResDate = new Label { Text = "Reservation Date:", Left = 20, Top = 55, Width = 100 };
            dtpReservationDate = new DateTimePicker { Left = 130, Top = 53, Width = 150 };

            var lblCheckIn = new Label { Text = "Check-In:", Left = 300, Top = 55, Width = 70 };
            dtpCheckIn = new DateTimePicker { Left = 380, Top = 53, Width = 150 };

            var lblCheckOut = new Label { Text = "Check-Out:", Left = 550, Top = 55, Width = 70 };
            dtpCheckOut = new DateTimePicker { Left = 630, Top = 53, Width = 150 };

            var lblGuests = new Label { Text = "Guests:", Left = 20, Top = 90, Width = 90 };
            numGuests = new NumericUpDown { Left = 120, Top = 88, Width = 60, Minimum = 1, Maximum = 20, Value = 1 };

            var lblResStatus = new Label { Text = "Status:", Left = 200, Top = 90, Width = 60 };
            cboResStatus = new ComboBox { Left = 260, Top = 88, Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
            cboResStatus.Items.AddRange(new[] { "Pending", "Confirmed", "CheckedIn", "CheckedOut", "Cancelled" });
            cboResStatus.SelectedIndex = 0;

            var lblRemarks = new Label { Text = "Remarks:", Left = 430, Top = 90, Width = 70 };
            txtRemarks = new TextBox { Left = 510, Top = 88, Width = 270 };

            lblReservationSelectedId = new Label { Text = "Selected ReservationId: (none)", Left = 20, Top = 120, Width = 300 };

            var btnLoad = new Button { Text = "Load Reservations", Left = 20, Top = 150, Width = 140 };
            var btnAdd = new Button { Text = "Add", Left = 170, Top = 150, Width = 80 };
            var btnUpdate = new Button { Text = "Update Selected", Left = 260, Top = 150, Width = 130 };
            var btnDelete = new Button { Text = "Delete Selected", Left = 400, Top = 150, Width = 130 };
            var btnClear = new Button { Text = "Clear Form", Left = 540, Top = 150, Width = 100 };

            dgvReservations = new DataGridView
            {
                Left = 20,
                Top = 185,
                Width = 780,
                Height = 315,
                ReadOnly = true,
                AllowUserToAddRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };

            btnLoad.Click += async (s, e) => await LoadReservationsTabDataAsync();
            btnAdd.Click += async (s, e) => await AddReservationAsync();
            btnUpdate.Click += async (s, e) => await UpdateReservationAsync();
            btnDelete.Click += async (s, e) => await DeleteReservationAsync();
            btnClear.Click += (s, e) => ClearReservationInputs();
            dgvReservations.SelectionChanged += DgvReservations_SelectionChanged;

            tab.Controls.Add(lblCustomer); tab.Controls.Add(cboResCustomer);
            tab.Controls.Add(lblRoom); tab.Controls.Add(cboResRoom);
            tab.Controls.Add(lblResDate); tab.Controls.Add(dtpReservationDate);
            tab.Controls.Add(lblCheckIn); tab.Controls.Add(dtpCheckIn);
            tab.Controls.Add(lblCheckOut); tab.Controls.Add(dtpCheckOut);
            tab.Controls.Add(lblGuests); tab.Controls.Add(numGuests);
            tab.Controls.Add(lblResStatus); tab.Controls.Add(cboResStatus);
            tab.Controls.Add(lblRemarks); tab.Controls.Add(txtRemarks);
            tab.Controls.Add(lblReservationSelectedId);
            tab.Controls.Add(btnLoad); tab.Controls.Add(btnAdd); tab.Controls.Add(btnUpdate);
            tab.Controls.Add(btnDelete); tab.Controls.Add(btnClear);
            tab.Controls.Add(dgvReservations);
        }

        private async Task LoadReservationsTabDataAsync()
        {
            await LoadCustomerAndRoomLookupsAsync();
            await LoadReservationsAsync();
        }

        private async Task LoadCustomerAndRoomLookupsAsync()
        {
            try
            {
                var custRequest = NewRequest(HttpMethod.Get, "customers");
                var custResponse = await _http.SendAsync(custRequest);
                custResponse.EnsureSuccessStatusCode();
                var custJson = await custResponse.Content.ReadAsStringAsync();
                var customers = JsonSerializer.Deserialize<List<CustomerLookupDto>>(custJson, JsonOptions) ?? new();

                var roomRequest = NewRequest(HttpMethod.Get, "rooms");
                var roomResponse = await _http.SendAsync(roomRequest);
                roomResponse.EnsureSuccessStatusCode();
                var roomJson = await roomResponse.Content.ReadAsStringAsync();
                var rooms = JsonSerializer.Deserialize<List<RoomDto>>(roomJson, JsonOptions) ?? new();

                cboResCustomer.Items.Clear();
                foreach (var c in customers)
                    cboResCustomer.Items.Add(new LookupItem { Id = c.CustomerId, Display = $"{c.CustomerId} - {c.CustomerName}" });

                cboResRoom.Items.Clear();
                foreach (var r in rooms)
                    cboResRoom.Items.Add(new LookupItem { Id = r.RoomId, Display = $"{r.RoomId} - {r.RoomNumber} ({r.RoomType})" });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to load customers/rooms for dropdowns: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task LoadReservationsAsync()
        {
            try
            {
                var request = NewRequest(HttpMethod.Get, "reservations");
                var response = await _http.SendAsync(request);
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                dgvReservations.DataSource = JsonSerializer.Deserialize<List<ReservationDto>>(json, JsonOptions);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to load reservations: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task AddReservationAsync()
        {
            if (cboResCustomer.SelectedItem is not LookupItem customer || cboResRoom.SelectedItem is not LookupItem room)
            {
                MessageBox.Show("Select a Customer and a Room (click 'Load Reservations' first if the lists are empty).", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (dtpCheckOut.Value <= dtpCheckIn.Value)
            {
                MessageBox.Show("Check-Out date must be after Check-In date.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var newReservation = new
            {
                customerId = customer.Id,
                roomId = room.Id,
                reservationDate = dtpReservationDate.Value,
                checkIn = dtpCheckIn.Value,
                checkOut = dtpCheckOut.Value,
                numberOfGuests = (int)numGuests.Value,
                status = cboResStatus.SelectedItem?.ToString() ?? "Pending",
                remarks = txtRemarks.Text.Trim()
            };

            try
            {
                var request = NewRequest(HttpMethod.Post, "reservations");
                request.Content = new StringContent(JsonSerializer.Serialize(newReservation), Encoding.UTF8, "application/json");
                var response = await _http.SendAsync(request);
                response.EnsureSuccessStatusCode();
                MessageBox.Show("Reservation added.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ClearReservationInputs();
                await LoadReservationsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to add reservation: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task UpdateReservationAsync()
        {
            if (dgvReservations.CurrentRow == null)
            {
                MessageBox.Show("Select a reservation first.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (cboResCustomer.SelectedItem is not LookupItem customer || cboResRoom.SelectedItem is not LookupItem room)
            {
                MessageBox.Show("Select a Customer and a Room.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (dtpCheckOut.Value <= dtpCheckIn.Value)
            {
                MessageBox.Show("Check-Out date must be after Check-In date.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var id = (int)dgvReservations.CurrentRow.Cells["ReservationId"].Value;
            var updated = new
            {
                customerId = customer.Id,
                roomId = room.Id,
                reservationDate = dtpReservationDate.Value,
                checkIn = dtpCheckIn.Value,
                checkOut = dtpCheckOut.Value,
                numberOfGuests = (int)numGuests.Value,
                status = cboResStatus.SelectedItem?.ToString() ?? "Pending",
                remarks = txtRemarks.Text.Trim()
            };

            try
            {
                var request = NewRequest(HttpMethod.Put, $"reservations/{id}");
                request.Content = new StringContent(JsonSerializer.Serialize(updated), Encoding.UTF8, "application/json");
                var response = await _http.SendAsync(request);
                response.EnsureSuccessStatusCode();
                MessageBox.Show("Reservation updated.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ClearReservationInputs();
                await LoadReservationsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to update reservation: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task DeleteReservationAsync()
        {
            if (dgvReservations.CurrentRow == null)
            {
                MessageBox.Show("Select a reservation first.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            var id = (int)dgvReservations.CurrentRow.Cells["ReservationId"].Value;
            if (MessageBox.Show("Delete this reservation?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            try
            {
                var request = NewRequest(HttpMethod.Delete, $"reservations/{id}");
                var response = await _http.SendAsync(request);
                response.EnsureSuccessStatusCode();
                MessageBox.Show("Reservation deleted.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ClearReservationInputs();
                await LoadReservationsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to delete reservation: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DgvReservations_SelectionChanged(object? sender, EventArgs e)
        {
            if (dgvReservations.CurrentRow == null) return;
            var row = dgvReservations.CurrentRow;

            var customerId = (int)row.Cells["CustomerId"].Value;
            var roomId = (int)row.Cells["RoomId"].Value;

            foreach (var item in cboResCustomer.Items)
                if (item is LookupItem li && li.Id == customerId) { cboResCustomer.SelectedItem = li; break; }

            foreach (var item in cboResRoom.Items)
                if (item is LookupItem li && li.Id == roomId) { cboResRoom.SelectedItem = li; break; }

            dtpReservationDate.Value = (DateTime)row.Cells["ReservationDate"].Value;
            dtpCheckIn.Value = (DateTime)row.Cells["CheckIn"].Value;
            dtpCheckOut.Value = (DateTime)row.Cells["CheckOut"].Value;
            numGuests.Value = Math.Max(1, (int)row.Cells["NumberOfGuests"].Value);
            cboResStatus.SelectedItem = row.Cells["Status"].Value?.ToString() ?? "Pending";
            txtRemarks.Text = row.Cells["Remarks"].Value?.ToString() ?? "";

            lblReservationSelectedId.Text = $"Selected ReservationId: {row.Cells["ReservationId"].Value}";
        }

        private void ClearReservationInputs()
        {
            cboResCustomer.SelectedIndex = -1;
            cboResRoom.SelectedIndex = -1;
            dtpReservationDate.Value = DateTime.Now;
            dtpCheckIn.Value = DateTime.Now;
            dtpCheckOut.Value = DateTime.Now.AddDays(1);
            numGuests.Value = 1;
            cboResStatus.SelectedIndex = 0;
            txtRemarks.Clear();
            lblReservationSelectedId.Text = "Selected ReservationId: (none)";
        }

        // ============================================================
        // CUSTOMERS TAB
        // ============================================================
        private void BuildCustomersTab(TabPage tab)
        {
            var lblCode = new Label { Text = "Customer Code:", Left = 20, Top = 20, Width = 100 };
            txtCustomerCode = new TextBox { Left = 130, Top = 18, Width = 150 };

            var lblName = new Label { Text = "Customer Name:", Left = 300, Top = 20, Width = 100 };
            txtCustomerName = new TextBox { Left = 400, Top = 18, Width = 200 };

            var lblContact = new Label { Text = "Contact #:", Left = 20, Top = 55, Width = 100 };
            txtContactNumber = new TextBox { Left = 130, Top = 53, Width = 150 };

            var lblEmail = new Label { Text = "Email:", Left = 300, Top = 55, Width = 100 };
            txtEmailAddress = new TextBox { Left = 400, Top = 53, Width = 200 };

            var lblAddress = new Label { Text = "Address:", Left = 20, Top = 90, Width = 100 };
            txtAddress = new TextBox { Left = 130, Top = 88, Width = 470 };

            chkCustomerActive = new CheckBox { Text = "Active", Left = 620, Top = 90, Width = 80, Checked = true };

            lblCustomerSelectedId = new Label { Text = "Selected CustomerId: (none)", Left = 20, Top = 120, Width = 300 };

            var btnLoad = new Button { Text = "Load Customers", Left = 20, Top = 150, Width = 130 };
            var btnAdd = new Button { Text = "Add", Left = 160, Top = 150, Width = 80 };
            var btnUpdate = new Button { Text = "Update Selected", Left = 250, Top = 150, Width = 130 };
            var btnDelete = new Button { Text = "Delete Selected", Left = 390, Top = 150, Width = 130 };
            var btnClear = new Button { Text = "Clear Form", Left = 530, Top = 150, Width = 100 };

            dgvCustomers = new DataGridView
            {
                Left = 20,
                Top = 185,
                Width = 780,
                Height = 315,
                ReadOnly = true,
                AllowUserToAddRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };

            btnLoad.Click += async (s, e) => await LoadCustomersAsync();
            btnAdd.Click += async (s, e) => await AddCustomerAsync();
            btnUpdate.Click += async (s, e) => await UpdateCustomerAsync();
            btnDelete.Click += async (s, e) => await DeleteCustomerAsync();
            btnClear.Click += (s, e) => ClearCustomerInputs();
            dgvCustomers.SelectionChanged += DgvCustomers_SelectionChanged;

            tab.Controls.Add(lblCode); tab.Controls.Add(txtCustomerCode);
            tab.Controls.Add(lblName); tab.Controls.Add(txtCustomerName);
            tab.Controls.Add(lblContact); tab.Controls.Add(txtContactNumber);
            tab.Controls.Add(lblEmail); tab.Controls.Add(txtEmailAddress);
            tab.Controls.Add(lblAddress); tab.Controls.Add(txtAddress);
            tab.Controls.Add(chkCustomerActive);
            tab.Controls.Add(lblCustomerSelectedId);
            tab.Controls.Add(btnLoad); tab.Controls.Add(btnAdd); tab.Controls.Add(btnUpdate);
            tab.Controls.Add(btnDelete); tab.Controls.Add(btnClear);
            tab.Controls.Add(dgvCustomers);
        }

        private async Task LoadCustomersAsync()
        {
            try
            {
                var request = NewRequest(HttpMethod.Get, "customers");
                var response = await _http.SendAsync(request);
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                dgvCustomers.DataSource = JsonSerializer.Deserialize<List<CustomerLookupDto>>(json, JsonOptions);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to load customers: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task AddCustomerAsync()
        {
            if (string.IsNullOrWhiteSpace(txtCustomerCode.Text) || string.IsNullOrWhiteSpace(txtCustomerName.Text))
            {
                MessageBox.Show("Customer Code and Customer Name are required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var newCustomer = new
            {
                customerCode = txtCustomerCode.Text.Trim(),
                customerName = txtCustomerName.Text.Trim(),
                contactNumber = txtContactNumber.Text.Trim(),
                emailAddress = txtEmailAddress.Text.Trim(),
                address = txtAddress.Text.Trim(),
                isActive = chkCustomerActive.Checked
            };

            try
            {
                var request = NewRequest(HttpMethod.Post, "customers");
                request.Content = new StringContent(JsonSerializer.Serialize(newCustomer), Encoding.UTF8, "application/json");
                var response = await _http.SendAsync(request);
                response.EnsureSuccessStatusCode();
                MessageBox.Show("Customer added.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ClearCustomerInputs();
                await LoadCustomersAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to add customer: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task UpdateCustomerAsync()
        {
            if (dgvCustomers.CurrentRow == null)
            {
                MessageBox.Show("Select a customer first.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(txtCustomerCode.Text) || string.IsNullOrWhiteSpace(txtCustomerName.Text))
            {
                MessageBox.Show("Customer Code and Customer Name are required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var id = (int)dgvCustomers.CurrentRow.Cells["CustomerId"].Value;
            var updated = new
            {
                customerCode = txtCustomerCode.Text.Trim(),
                customerName = txtCustomerName.Text.Trim(),
                contactNumber = txtContactNumber.Text.Trim(),
                emailAddress = txtEmailAddress.Text.Trim(),
                address = txtAddress.Text.Trim(),
                isActive = chkCustomerActive.Checked
            };

            try
            {
                var request = NewRequest(HttpMethod.Put, $"customers/{id}");
                request.Content = new StringContent(JsonSerializer.Serialize(updated), Encoding.UTF8, "application/json");
                var response = await _http.SendAsync(request);
                response.EnsureSuccessStatusCode();
                MessageBox.Show("Customer updated.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ClearCustomerInputs();
                await LoadCustomersAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to update customer: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task DeleteCustomerAsync()
        {
            if (dgvCustomers.CurrentRow == null)
            {
                MessageBox.Show("Select a customer first.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            var id = (int)dgvCustomers.CurrentRow.Cells["CustomerId"].Value;
            if (MessageBox.Show("Delete this customer?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            try
            {
                var request = NewRequest(HttpMethod.Delete, $"customers/{id}");
                var response = await _http.SendAsync(request);
                response.EnsureSuccessStatusCode();
                MessageBox.Show("Customer deleted.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ClearCustomerInputs();
                await LoadCustomersAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to delete customer: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DgvCustomers_SelectionChanged(object? sender, EventArgs e)
        {
            if (dgvCustomers.CurrentRow == null) return;
            var row = dgvCustomers.CurrentRow;
            txtCustomerCode.Text = row.Cells["CustomerCode"].Value?.ToString() ?? "";
            txtCustomerName.Text = row.Cells["CustomerName"].Value?.ToString() ?? "";
            txtContactNumber.Text = row.Cells["ContactNumber"].Value?.ToString() ?? "";
            txtEmailAddress.Text = row.Cells["EmailAddress"].Value?.ToString() ?? "";
            txtAddress.Text = row.Cells["Address"].Value?.ToString() ?? "";
            chkCustomerActive.Checked = row.Cells["IsActive"].Value is bool b && b;
            lblCustomerSelectedId.Text = $"Selected CustomerId: {row.Cells["CustomerId"].Value}";
        }

        private void ClearCustomerInputs()
        {
            txtCustomerCode.Clear(); txtCustomerName.Clear(); txtContactNumber.Clear();
            txtEmailAddress.Clear(); txtAddress.Clear();
            chkCustomerActive.Checked = true;
            lblCustomerSelectedId.Text = "Selected CustomerId: (none)";
        }
    }

    internal class ProductDto
    {
        public int ProductId { get; set; }
        public string ProductCode { get; set; } = "";
        public string ProductName { get; set; } = "";
        public decimal Price { get; set; }
        public int StockQuantity { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    internal class RoomDto
    {
        public int RoomId { get; set; }
        public string RoomNumber { get; set; } = "";
        public string RoomType { get; set; } = "";
        public decimal Rate { get; set; }
        public string Status { get; set; } = "";
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    internal class ReservationDto
    {
        public int ReservationId { get; set; }
        public int CustomerId { get; set; }
        public int RoomId { get; set; }
        public DateTime ReservationDate { get; set; }
        public DateTime CheckIn { get; set; }
        public DateTime CheckOut { get; set; }
        public int NumberOfGuests { get; set; }
        public string Status { get; set; } = "";
        public string Remarks { get; set; } = "";
        public DateTime CreatedAt { get; set; }
    }

    internal class CustomerLookupDto
    {
        public int CustomerId { get; set; }
        public string CustomerCode { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public string ContactNumber { get; set; } = "";
        public string EmailAddress { get; set; } = "";
        public string Address { get; set; } = "";
        public bool IsActive { get; set; }
    }

    internal class LookupItem
    {
        public int Id { get; set; }
        public string Display { get; set; } = "";
        public override string ToString() => Display;
    }
}