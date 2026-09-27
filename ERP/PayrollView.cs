using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ERP
{
    internal class EmployeeDto
    {
        public int EmployeeId { get; set; }
        public string EmployeeCode { get; set; } = "";
        public string FullName { get; set; } = "";
        public string? Position { get; set; }
        public decimal DailyRate { get; set; }
        public DateTime DateHired { get; set; }
        public bool IsActive { get; set; }
    }

    internal class PayrollRecordDto
    {
        public int PayrollRecordId { get; set; }
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; } = "";
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public decimal DaysWorked { get; set; }
        public decimal DailyRate { get; set; }
        public decimal GrossPay { get; set; }
        public decimal Deductions { get; set; }
        public decimal NetPay { get; set; }
    }

    public class PayrollView : UserControl
    {
        // Default daily rate per position (₱). Edit these to match the resort's pay scale.
        private static readonly Dictionary<string, decimal> PositionRates = new()
        {
            { "Staff", 450m },
            { "Waiter", 450m },
            { "Housekeeping", 500m },
            { "Front Desk", 550m },
            { "Cook", 600m },
            { "Supervisor", 750m },
            { "Manager", 1000m },
        };

        private readonly ApiClient _api;

        // Employees
        private readonly TextBox txtCode = new(), txtName = new();
        private readonly ComboBox cboPosition = new() { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly NumericUpDown numDailyRate = new() { Minimum = 0, Maximum = 1000000, DecimalPlaces = 2, ThousandsSeparator = true };
        private readonly DateTimePicker dtpHired = new() { Format = DateTimePickerFormat.Short };
        private readonly CheckBox chkActive = new() { Text = "Active", Checked = true };
        private readonly DataGridView gridEmployees = UiKit.Grid();
        private int? _selectedEmployeeId;

        // Payroll
        private readonly ComboBox cboEmployee = new() { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly DateTimePicker dtpStart = new() { Format = DateTimePickerFormat.Short };
        private readonly DateTimePicker dtpEnd = new() { Format = DateTimePickerFormat.Short };
        private readonly NumericUpDown numDays = new() { Minimum = 0, Maximum = 31, DecimalPlaces = 1, Increment = 0.5m };
        private readonly NumericUpDown numDeductions = new() { Minimum = 0, Maximum = 1000000, DecimalPlaces = 2, ThousandsSeparator = true };
        private readonly Label lblPreview = new() { Font = AppTheme.BodyFont, ForeColor = AppTheme.TextDark };
        private readonly DataGridView gridPayroll = UiKit.Grid();

        public PayrollView(string companyCode)
        {
            _api = new ApiClient(companyCode);
            BackColor = AppTheme.Background;

            var tabs = new TabControl { Dock = DockStyle.Fill, Font = AppTheme.BodyFont };
            tabs.TabPages.Add(BuildEmployeesPage());
            tabs.TabPages.Add(BuildPayrollPage());
            Controls.Add(tabs);

            Load += async (s, e) =>
            {
                await LoadEmployeesAsync();
                await LoadPayrollAsync();
            };
        }

        // ---------------- EMPLOYEES ----------------
        private TabPage BuildEmployeesPage()
        {
            var page = new TabPage("Employees") { BackColor = AppTheme.Background };
            var top = new Panel();

            cboPosition.Items.AddRange(PositionRates.Keys.Cast<object>().ToArray());
            cboPosition.SelectedIndexChanged += (s, e) =>
            {
                // Picking a position fills in its default daily rate (still editable)
                if (cboPosition.SelectedItem is string pos && PositionRates.TryGetValue(pos, out var rate))
                    numDailyRate.Value = rate;
            };

            UiKit.AddField(top, "Employee Code *", txtCode, 20, 150);
            UiKit.AddField(top, "Full Name *", txtName, 180, 220);
            UiKit.AddField(top, "Position *", cboPosition, 410, 160);
            UiKit.AddField(top, "Daily Rate (₱) *", numDailyRate, 580, 120);
            UiKit.AddField(top, "Date Hired", dtpHired, 710, 120);

            chkActive.SetBounds(20, 82, 90, 24);
            chkActive.Font = AppTheme.BodyFont;
            top.Controls.Add(chkActive);

            var btnAdd = UiKit.ActionButton("+", "Add", 120, 76);
            var btnUpdate = UiKit.ActionButton("✎", "Update", 260, 76);
            var btnDelete = UiKit.ActionButton("✕", "Delete", 400, 76, 130, false);
            var btnClear = UiKit.ActionButton("⟲", "Clear", 540, 76, 130, false);
            btnAdd.Click += async (s, e) => await SaveEmployeeAsync(isNew: true);
            btnUpdate.Click += async (s, e) => await SaveEmployeeAsync(isNew: false);
            btnDelete.Click += async (s, e) => await DeleteEmployeeAsync();
            btnClear.Click += (s, e) => ClearEmployee();
            top.Controls.AddRange(new Control[] { btnAdd, btnUpdate, btnDelete, btnClear });

            gridEmployees.CellClick += (s, e) =>
            {
                if (e.RowIndex >= 0 && gridEmployees.Rows[e.RowIndex].DataBoundItem is EmployeeDto d) FillEmployee(d);
            };

            UiKit.StackLayout(page, top, 125, gridEmployees);
            return page;
        }

        private async Task LoadEmployeesAsync()
        {
            try
            {
                var employees = await _api.GetAsync<List<EmployeeDto>>("employees");
                gridEmployees.DataSource = employees;
                UiKit.HideColumns(gridEmployees, "EmployeeId");
                UiKit.FormatMoney(gridEmployees, "DailyRate");
                UiKit.FormatDate(gridEmployees, "DateHired");

                var previous = (cboEmployee.SelectedItem as OptionItem)?.Id;
                cboEmployee.Items.Clear();
                foreach (var emp in employees.Where(x => x.IsActive))
                    cboEmployee.Items.Add(new OptionItem(emp.EmployeeId, $"{emp.FullName} - {emp.Position} ({UiKit.Peso(emp.DailyRate)}/day)", emp.DailyRate));
                foreach (OptionItem item in cboEmployee.Items)
                    if (item.Id == previous) cboEmployee.SelectedItem = item;
            }
            catch (Exception ex) { UiKit.ShowError(ex); }
        }

        private void FillEmployee(EmployeeDto d)
        {
            _selectedEmployeeId = d.EmployeeId;
            txtCode.Text = d.EmployeeCode;
            txtName.Text = d.FullName;

            // Select the position without overwriting the employee's actual saved rate
            if (d.Position != null && cboPosition.Items.Contains(d.Position))
                cboPosition.SelectedItem = d.Position;
            else
                cboPosition.SelectedIndex = -1;

            numDailyRate.Value = Math.Min(numDailyRate.Maximum, d.DailyRate);
            if (d.DateHired > dtpHired.MinDate) dtpHired.Value = d.DateHired;
            chkActive.Checked = d.IsActive;
        }

        private void ClearEmployee()
        {
            _selectedEmployeeId = null;
            txtCode.Clear();
            txtName.Clear();
            cboPosition.SelectedIndex = -1;
            numDailyRate.Value = 0;
            dtpHired.Value = DateTime.Today;
            chkActive.Checked = true;
            gridEmployees.ClearSelection();
        }

        private async Task SaveEmployeeAsync(bool isNew)
        {
            if (string.IsNullOrWhiteSpace(txtCode.Text) || string.IsNullOrWhiteSpace(txtName.Text))
            {
                UiKit.Warn("Employee Code and Full Name are required.");
                return;
            }
            if (cboPosition.SelectedItem is not string position)
            {
                UiKit.Warn("Please select a Position.");
                return;
            }
            if (numDailyRate.Value <= 0)
            {
                UiKit.Warn("Daily Rate must be greater than zero.");
                return;
            }
            if (!isNew && _selectedEmployeeId is null)
            {
                UiKit.Warn("Select an employee from the list first.");
                return;
            }

            var body = new
            {
                employeeCode = txtCode.Text.Trim(),
                fullName = txtName.Text.Trim(),
                position,
                dailyRate = numDailyRate.Value,
                dateHired = dtpHired.Value.Date,
                isActive = chkActive.Checked
            };

            try
            {
                if (isNew) await _api.PostAsync("employees", body);
                else await _api.PutAsync($"employees/{_selectedEmployeeId}", body);
                ClearEmployee();
                await LoadEmployeesAsync();
            }
            catch (Exception ex) { UiKit.ShowError(ex); }
        }

        private async Task DeleteEmployeeAsync()
        {
            if (_selectedEmployeeId is null)
            {
                UiKit.Warn("Select an employee from the list first.");
                return;
            }
            if (!UiKit.Confirm($"Delete employee \"{txtName.Text}\"?")) return;

            try
            {
                await _api.DeleteAsync($"employees/{_selectedEmployeeId}");
                ClearEmployee();
                await LoadEmployeesAsync();
            }
            catch (Exception ex) { UiKit.ShowError(ex); }
        }

        // ---------------- PAYROLL ----------------
        private TabPage BuildPayrollPage()
        {
            var page = new TabPage("Payroll") { BackColor = AppTheme.Background };
            var top = new Panel();

            // Default to the current semi-monthly cut-off (1–15 or 16–end of month)
            var today = DateTime.Today;
            var monthStart = new DateTime(today.Year, today.Month, 1);
            dtpStart.Value = today.Day <= 15 ? monthStart : monthStart.AddDays(15);
            dtpEnd.Value = today.Day <= 15 ? monthStart.AddDays(14) : monthStart.AddMonths(1).AddDays(-1);

            UiKit.AddField(top, "Employee *", cboEmployee, 20, 260);
            UiKit.AddField(top, "Period Start", dtpStart, 290, 130);
            UiKit.AddField(top, "Period End", dtpEnd, 430, 130);
            UiKit.AddField(top, "Days Worked *", numDays, 570, 110);
            UiKit.AddField(top, "Deductions (₱)", numDeductions, 690, 130);

            var btnSave = UiKit.ActionButton("₱", "Compute & Save", 20, 76, 180);
            var btnDelete = UiKit.ActionButton("✕", "Delete", 210, 76, 120, false);
            btnSave.Click += async (s, e) => await SavePayrollAsync();
            btnDelete.Click += async (s, e) => await DeletePayrollAsync();
            top.Controls.AddRange(new Control[] { btnSave, btnDelete });

            lblPreview.SetBounds(345, 84, 500, 24);
            top.Controls.Add(lblPreview);
            cboEmployee.SelectedIndexChanged += (s, e) => UpdatePreview();
            numDays.ValueChanged += (s, e) => UpdatePreview();
            numDeductions.ValueChanged += (s, e) => UpdatePreview();
            UpdatePreview();

            UiKit.StackLayout(page, top, 125, gridPayroll);
            return page;
        }

        private void UpdatePreview()
        {
            var rate = (cboEmployee.SelectedItem as OptionItem)?.Extra ?? 0m;
            var gross = Math.Round(rate * numDays.Value, 2);
            var net = gross - numDeductions.Value;
            lblPreview.Text = $"Gross {UiKit.Peso(gross)}  −  Deductions {UiKit.Peso(numDeductions.Value)}  =  Net {UiKit.Peso(net)}";
            lblPreview.ForeColor = net < 0 ? UiKit.Red : AppTheme.TextDark;
        }

        private async Task LoadPayrollAsync()
        {
            try
            {
                var records = await _api.GetAsync<List<PayrollRecordDto>>("payroll");
                gridPayroll.DataSource = records;
                UiKit.HideColumns(gridPayroll, "PayrollRecordId", "EmployeeId");
                UiKit.FormatMoney(gridPayroll, "DailyRate", "GrossPay", "Deductions", "NetPay");
                UiKit.FormatDate(gridPayroll, "PeriodStart", "PeriodEnd");
            }
            catch (Exception ex) { UiKit.ShowError(ex); }
        }

        private async Task SavePayrollAsync()
        {
            if (cboEmployee.SelectedItem is not OptionItem employee)
            {
                UiKit.Warn("Please select an employee. (Add one in the Employees tab first.)");
                return;
            }
            if (dtpEnd.Value.Date < dtpStart.Value.Date)
            {
                UiKit.Warn("Period End must be on or after Period Start.");
                return;
            }
            if (numDays.Value <= 0)
            {
                UiKit.Warn("Days Worked must be greater than zero.");
                return;
            }

            var body = new
            {
                employeeId = employee.Id,
                periodStart = dtpStart.Value.Date,
                periodEnd = dtpEnd.Value.Date,
                daysWorked = numDays.Value,
                deductions = numDeductions.Value
            };

            try
            {
                await _api.PostAsync("payroll", body);
                numDays.Value = 0;
                numDeductions.Value = 0;
                await LoadPayrollAsync();
            }
            catch (Exception ex) { UiKit.ShowError(ex); }
        }

        private async Task DeletePayrollAsync()
        {
            if (gridPayroll.CurrentRow?.DataBoundItem is not PayrollRecordDto record)
            {
                UiKit.Warn("Select a payroll record from the list first.");
                return;
            }
            if (!UiKit.Confirm($"Delete payroll for {record.EmployeeName} ({record.PeriodStart:MMM d} – {record.PeriodEnd:MMM d})?")) return;

            try
            {
                await _api.DeleteAsync($"payroll/{record.PayrollRecordId}");
                await LoadPayrollAsync();
            }
            catch (Exception ex) { UiKit.ShowError(ex); }
        }
    }
}