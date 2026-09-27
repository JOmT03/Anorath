using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
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
    public class EmployeeRow
    {
        public int EmployeeId { get; set; }
        public string EmployeeCode { get; set; } = "";
        public string FullName { get; set; } = "";
        public string? Position { get; set; }
        public decimal DailyRate { get; set; }
        public DateTime DateHired { get; set; }
        public bool IsActive { get; set; }

        public string PositionText => string.IsNullOrWhiteSpace(Position) ? "—" : Position!;
        public string RateText => "₱" + DailyRate.ToString("N2");
        public string HiredText => DateHired.ToString("MMM dd, yyyy");
        public string ActiveText => IsActive ? "Yes" : "No";
    }

    public class PayrollRow
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

        public string PeriodText => $"{PeriodStart:MMM dd} – {PeriodEnd:MMM dd, yyyy}";
        public string DaysText => DaysWorked.ToString("0.##");
        public string RateText => "₱" + DailyRate.ToString("N2");
        public string GrossText => "₱" + GrossPay.ToString("N2");
        public string DeductionsText => "₱" + Deductions.ToString("N2");
        public string NetText => "₱" + NetPay.ToString("N2");
    }

    public sealed partial class PayrollPage : Page
    {
        private static readonly string[] Positions =
            { "Front Desk Officer", "Housekeeper", "Cook", "Kitchen Helper", "Waiter", "Cashier", "Maintenance", "Security Guard", "Manager" };

        private readonly ObservableCollection<EmployeeRow> _employeeView = new();
        private readonly ObservableCollection<PayrollRow> _payrollView = new();
        private List<EmployeeRow> _employees = new();
        private List<PayrollRow> _payroll = new();
        private bool _ready;

        public PayrollPage()
        {
            InitializeComponent();
            EmployeesList.ItemsSource = _employeeView;
            PayrollList.ItemsSource = _payrollView;
            Loaded += async (s, e) => await LoadAllAsync();
        }

        private async Task LoadAllAsync()
        {
            _ready = false;
            await LoadEmployeesAsync();
            await LoadPayrollAsync();
            _ready = true;
        }

        // ================= EMPLOYEES =================
        private async Task LoadEmployeesAsync()
        {
            try
            {
                _employees = await ApiClient.GetAsync<List<EmployeeRow>>("employees");
                ApplyEmployeeSearch();

                // Filter for the payroll tab
                var keep = (EmployeeFilter.SelectedItem as EmployeeRow)?.EmployeeId ?? 0;
                var options = new List<EmployeeRow> { new EmployeeRow { EmployeeId = 0, FullName = "All employees" } };
                options.AddRange(_employees);
                EmployeeFilter.ItemsSource = options;
                EmployeeFilter.SelectedItem = options.FirstOrDefault(o => o.EmployeeId == keep) ?? options[0];
            }
            catch (Exception ex)
            {
                Show(InfoBarSeverity.Error, ex.Message);
            }
        }

        private void ApplyEmployeeSearch()
        {
            var s = EmpSearchBox.Text.Trim();
            var list = string.IsNullOrEmpty(s)
                ? _employees
                : _employees.Where(e => e.EmployeeCode.Contains(s, StringComparison.OrdinalIgnoreCase)
                                     || e.FullName.Contains(s, StringComparison.OrdinalIgnoreCase)
                                     || (e.Position ?? "").Contains(s, StringComparison.OrdinalIgnoreCase)).ToList();

            _employeeView.Clear();
            foreach (var e in list) _employeeView.Add(e);
            EmptyEmployeesText.Visibility = _employeeView.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void EmpSearch_Click(object sender, RoutedEventArgs e) => ApplyEmployeeSearch();

        private void EmpSearchBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter) ApplyEmployeeSearch();
        }

        private async void AddEmployee_Click(object sender, RoutedEventArgs e)
        {
            if (await ShowEmployeeDialogAsync(null))
            {
                Show(InfoBarSeverity.Success, "Employee added.");
                await LoadEmployeesAsync();
            }
        }

        private async void EditEmployee_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement { Tag: int id }) return;
            var emp = _employees.FirstOrDefault(x => x.EmployeeId == id);
            if (emp == null) return;

            if (await ShowEmployeeDialogAsync(emp))
            {
                Show(InfoBarSeverity.Success, "Employee updated.");
                await LoadEmployeesAsync();
                await LoadPayrollAsync();
            }
        }

        private async void DeleteEmployee_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement { Tag: int id }) return;
            var emp = _employees.FirstOrDefault(x => x.EmployeeId == id);
            if (emp == null) return;

            if (!await ConfirmAsync("Delete employee?", $"Delete {emp.FullName} ({emp.EmployeeCode})?", "Delete")) return;

            try
            {
                await ApiClient.DeleteAsync($"employees/{id}");
                Show(InfoBarSeverity.Success, "Employee deleted.");
                await LoadEmployeesAsync();
            }
            catch (Exception ex)
            {
                Show(InfoBarSeverity.Error, ex.Message);   // e.g. "has payroll records. Set them to inactive..."
            }
        }

        private string NextEmployeeCode()
        {
            var n = _employees.Count + 1;
            string code;
            do { code = $"EMP-{n:0000}"; n++; }
            while (_employees.Any(e => e.EmployeeCode.Equals(code, StringComparison.OrdinalIgnoreCase)));
            return code;
        }

        private async Task<bool> ShowEmployeeDialogAsync(EmployeeRow? existing)
        {
            var codeBox = new TextBox { Header = "Employee code", Text = existing?.EmployeeCode ?? NextEmployeeCode(), MaxLength = 30 };
            var nameBox = new TextBox { Header = "Full name", Text = existing?.FullName ?? "", MaxLength = 150 };
            var positionBox = new ComboBox { Header = "Position", ItemsSource = Positions, IsEditable = true, Text = existing?.Position ?? "", HorizontalAlignment = HorizontalAlignment.Stretch };
            var rateBox = new NumberBox { Header = "Daily rate (₱)", Minimum = 0, Value = existing != null ? (double)existing.DailyRate : 610, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
            var hiredBox = new CalendarDatePicker { Header = "Date hired", Date = existing?.DateHired ?? DateTime.Today, MaxDate = DateTimeOffset.Now, HorizontalAlignment = HorizontalAlignment.Stretch };
            var activeBox = new CheckBox { Content = "Active", IsChecked = existing?.IsActive ?? true };
            var errorText = new TextBlock { Foreground = new SolidColorBrush(Colors.Firebrick), TextWrapping = TextWrapping.Wrap };

            var form = new StackPanel { Spacing = 10, Width = 360 };
            form.Children.Add(codeBox);
            form.Children.Add(nameBox);
            form.Children.Add(positionBox);
            form.Children.Add(rateBox);
            form.Children.Add(hiredBox);
            form.Children.Add(activeBox);
            form.Children.Add(errorText);

            var dialog = new ContentDialog
            {
                Title = existing == null ? "Add Employee" : $"Edit {existing.FullName}",
                Content = new ScrollViewer { Content = form, MaxHeight = 520 },
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
                    var code = codeBox.Text.Trim();
                    var name = nameBox.Text.Trim();
                    var position = ((positionBox.SelectedItem as string) ?? positionBox.Text ?? "").Trim();

                    if (code.Length == 0 || name.Length < 2)
                    { errorText.Text = "Employee code and full name are required."; args.Cancel = true; return; }
                    if (_employees.Any(x => x.EmployeeId != (existing?.EmployeeId ?? 0) && x.EmployeeCode.Equals(code, StringComparison.OrdinalIgnoreCase)))
                    { errorText.Text = "That employee code is already used."; args.Cancel = true; return; }
                    if (double.IsNaN(rateBox.Value) || rateBox.Value <= 0)
                    { errorText.Text = "Daily rate must be greater than zero."; args.Cancel = true; return; }

                    var body = new
                    {
                        employeeCode = code,
                        fullName = name,
                        position,
                        dailyRate = (decimal)rateBox.Value,
                        dateHired = (hiredBox.Date?.Date ?? DateTime.Today).ToString("yyyy-MM-dd"),
                        isActive = activeBox.IsChecked == true
                    };

                    if (existing == null) await ApiClient.PostAsync("employees", body);
                    else await ApiClient.PutAsync($"employees/{existing.EmployeeId}", body);
                }
                catch (Exception ex)
                {
                    errorText.Text = ex.Message;
                    args.Cancel = true;
                }
                finally
                {
                    deferral.Complete();
                }
            };

            return await dialog.ShowAsync() == ContentDialogResult.Primary;
        }

        // ================= PAYROLL =================
        private async Task LoadPayrollAsync()
        {
            try
            {
                _payroll = await ApiClient.GetAsync<List<PayrollRow>>("payroll");
                ApplyPayrollFilter();
            }
            catch (Exception ex)
            {
                Show(InfoBarSeverity.Error, ex.Message);
            }
        }

        private void ApplyPayrollFilter()
        {
            var id = (EmployeeFilter.SelectedItem as EmployeeRow)?.EmployeeId ?? 0;
            var list = id > 0 ? _payroll.Where(p => p.EmployeeId == id).ToList() : _payroll;

            _payrollView.Clear();
            foreach (var p in list) _payrollView.Add(p);
            EmptyPayrollText.Visibility = _payrollView.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            PayrollSummaryText.Text = $"{list.Count} record(s)   ·   Gross: ₱{list.Sum(p => p.GrossPay):N2}   ·   Deductions: ₱{list.Sum(p => p.Deductions):N2}   ·   Net: ₱{list.Sum(p => p.NetPay):N2}";
        }

        private void EmployeeFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_ready) ApplyPayrollFilter();
        }

        private async void RunPayroll_Click(object sender, RoutedEventArgs e) => await RunPayrollAsync(null);

        private async void RunPayrollFor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { Tag: int id }) await RunPayrollAsync(id);
        }

        private async Task RunPayrollAsync(int? employeeId)
        {
            var active = _employees.Where(x => x.IsActive).ToList();
            if (active.Count == 0)
            {
                Show(InfoBarSeverity.Warning, "Add an active employee first.");
                return;
            }

            // Default: semi-monthly period (1–15 or 16–end of month)
            var today = DateTime.Today;
            var start = today.Day <= 15 ? new DateTime(today.Year, today.Month, 1) : new DateTime(today.Year, today.Month, 16);
            var end = today.Day <= 15 ? new DateTime(today.Year, today.Month, 15) : new DateTime(today.Year, today.Month, 1).AddMonths(1).AddDays(-1);

            var empBox = new ComboBox { Header = "Employee", ItemsSource = active, DisplayMemberPath = "FullName", HorizontalAlignment = HorizontalAlignment.Stretch };
            empBox.SelectedItem = active.FirstOrDefault(x => x.EmployeeId == employeeId) ?? active[0];
            var startBox = new CalendarDatePicker { Header = "Period start", Date = start, HorizontalAlignment = HorizontalAlignment.Stretch };
            var endBox = new CalendarDatePicker { Header = "Period end", Date = end, HorizontalAlignment = HorizontalAlignment.Stretch };
            var daysBox = new NumberBox { Header = "Days worked", Minimum = 0, Maximum = 31, Value = 13, SmallChange = 0.5, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
            var dedBox = new NumberBox { Header = "Deductions (SSS, PhilHealth, Pag-IBIG, etc.)", Minimum = 0, Value = 0, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
            var preview = new TextBlock { FontSize = 15, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
            var errorText = new TextBlock { Foreground = new SolidColorBrush(Colors.Firebrick), TextWrapping = TextWrapping.Wrap };

            void UpdatePreview()
            {
                if (empBox.SelectedItem is not EmployeeRow emp) { preview.Text = ""; return; }
                var days = double.IsNaN(daysBox.Value) ? 0 : (decimal)daysBox.Value;
                var ded = double.IsNaN(dedBox.Value) ? 0 : (decimal)dedBox.Value;
                var gross = Math.Round(emp.DailyRate * days, 2);
                preview.Text = $"Gross: ₱{emp.DailyRate:N2} × {days:0.##} = ₱{gross:N2}\nNet pay: ₱{gross - ded:N2}";
            }
            empBox.SelectionChanged += (s, a) => UpdatePreview();
            daysBox.ValueChanged += (s, a) => UpdatePreview();
            dedBox.ValueChanged += (s, a) => UpdatePreview();
            UpdatePreview();

            var dates = new Grid { ColumnSpacing = 10 };
            dates.ColumnDefinitions.Add(new ColumnDefinition());
            dates.ColumnDefinitions.Add(new ColumnDefinition());
            dates.Children.Add(startBox);
            Grid.SetColumn(endBox, 1);
            dates.Children.Add(endBox);

            var form = new StackPanel { Spacing = 10, Width = 380 };
            form.Children.Add(empBox);
            form.Children.Add(dates);
            form.Children.Add(daysBox);
            form.Children.Add(dedBox);
            form.Children.Add(preview);
            form.Children.Add(errorText);

            var dialog = new ContentDialog
            {
                Title = "Run Payroll",
                Content = new ScrollViewer { Content = form, MaxHeight = 540 },
                PrimaryButtonText = "Save payroll",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot
            };

            dialog.PrimaryButtonClick += async (s, args) =>
            {
                var deferral = args.GetDeferral();
                try
                {
                    if (empBox.SelectedItem is not EmployeeRow emp || startBox.Date is null || endBox.Date is null)
                    { errorText.Text = "Employee and both dates are required."; args.Cancel = true; return; }

                    var ps = startBox.Date.Value.Date;
                    var pe = endBox.Date.Value.Date;
                    var days = double.IsNaN(daysBox.Value) ? 0 : (decimal)daysBox.Value;
                    var ded = double.IsNaN(dedBox.Value) ? 0 : (decimal)dedBox.Value;

                    if (pe < ps) { errorText.Text = "Period end must be on or after period start."; args.Cancel = true; return; }
                    if (days <= 0) { errorText.Text = "Days worked must be greater than zero."; args.Cancel = true; return; }
                    if (days > (pe - ps).Days + 1) { errorText.Text = $"Days worked cannot exceed the {(pe - ps).Days + 1} days in the period."; args.Cancel = true; return; }
                    if (ded > emp.DailyRate * days) { errorText.Text = "Deductions cannot be greater than gross pay."; args.Cancel = true; return; }

                    await ApiClient.PostAsync("payroll", new
                    {
                        employeeId = emp.EmployeeId,
                        periodStart = ps.ToString("yyyy-MM-dd"),
                        periodEnd = pe.ToString("yyyy-MM-dd"),
                        daysWorked = days,
                        deductions = ded
                    });
                }
                catch (Exception ex)
                {
                    errorText.Text = ex.Message;
                    args.Cancel = true;
                }
                finally
                {
                    deferral.Complete();
                }
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                Show(InfoBarSeverity.Success, "Payroll saved. It is now counted under Salaries & Wages in the Financial Statements.");
                await LoadPayrollAsync();
            }
        }

        private async void DeletePayroll_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement { Tag: int id }) return;
            var rec = _payroll.FirstOrDefault(p => p.PayrollRecordId == id);
            if (rec == null) return;

            if (!await ConfirmAsync("Delete payroll record?", $"Delete the payroll of {rec.EmployeeName} for {rec.PeriodText}?", "Delete")) return;

            try
            {
                await ApiClient.DeleteAsync($"payroll/{id}");
                Show(InfoBarSeverity.Success, "Payroll record deleted.");
                await LoadPayrollAsync();
            }
            catch (Exception ex)
            {
                Show(InfoBarSeverity.Error, ex.Message);
            }
        }

        // ================= PAYSLIP (print / PDF) =================
        private void PrintPayslip_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement { Tag: int id }) return;
            var p = _payroll.FirstOrDefault(x => x.PayrollRecordId == id);
            if (p == null) return;
            var emp = _employees.FirstOrDefault(x => x.EmployeeId == p.EmployeeId);

            static string H(string? s) => WebUtility.HtmlEncode(s ?? "");
            var slipNo = $"PS-{p.PayrollRecordId:00000}";

            var sb = new StringBuilder();
            sb.Append("<!doctype html><html><head><meta charset='utf-8'><title>").Append(slipNo).Append("</title><style>");
            sb.Append(@"
body{font-family:'Segoe UI',Arial,sans-serif;color:#222;margin:40px auto;max-width:620px;font-size:14px;}
.top{border-bottom:3px solid #1f4e79;padding-bottom:10px;display:flex;justify-content:space-between;align-items:flex-end;}
.company{font-size:22px;font-weight:700;color:#1f4e79;} .title{font-size:18px;font-weight:700;}
table{width:100%;border-collapse:collapse;margin-top:18px;} td{padding:7px 4px;} td.a{text-align:right;}
.k{color:#555;width:180px;} .sec td{font-weight:700;border-bottom:1px solid #ccd;padding-top:16px;}
.net td{font-weight:700;font-size:17px;border-top:2px solid #333;}
.sign{display:flex;justify-content:space-between;margin-top:60px;} .sign div{width:44%;border-top:1px solid #333;text-align:center;padding-top:6px;}
.small{color:#666;font-size:11px;margin-top:24px;}
");
            sb.Append("</style></head><body>");
            sb.Append("<div class='top'><div class='company'>").Append(H(Session.CompanyName)).Append("</div><div class='title'>PAYSLIP · ").Append(slipNo).Append("</div></div>");

            sb.Append("<table>");
            sb.Append("<tr><td class='k'>Employee</td><td><b>").Append(H(p.EmployeeName)).Append("</b>").Append(emp != null ? $" ({H(emp.EmployeeCode)})" : "").Append("</td></tr>");
            sb.Append("<tr><td class='k'>Position</td><td>").Append(H(emp?.PositionText)).Append("</td></tr>");
            sb.Append("<tr><td class='k'>Pay period</td><td>").Append(p.PeriodStart.ToString("MMMM dd, yyyy")).Append(" – ").Append(p.PeriodEnd.ToString("MMMM dd, yyyy")).Append("</td></tr>");
            sb.Append("</table><table>");
            sb.Append("<tr class='sec'><td>EARNINGS</td><td class='a'></td></tr>");
            sb.Append("<tr><td>Basic pay (₱").Append(p.DailyRate.ToString("N2")).Append(" × ").Append(p.DaysText).Append(" days)</td><td class='a'>₱").Append(p.GrossPay.ToString("N2")).Append("</td></tr>");
            sb.Append("<tr class='sec'><td>DEDUCTIONS</td><td class='a'></td></tr>");
            sb.Append("<tr><td>SSS, PhilHealth, Pag-IBIG &amp; others</td><td class='a'>(₱").Append(p.Deductions.ToString("N2")).Append(")</td></tr>");
            sb.Append("<tr class='net'><td>NET PAY</td><td class='a'>₱").Append(p.NetPay.ToString("N2")).Append("</td></tr>");
            sb.Append("</table>");

            sb.Append("<div class='sign'><div>Prepared by: ").Append(H(Session.FullName)).Append("</div><div>Received by: ").Append(H(p.EmployeeName)).Append("</div></div>");
            sb.Append("<div class='small'>Generated ").Append(DateTime.Now.ToString("MMM dd, yyyy h:mm tt")).Append(". This payslip is system-generated.</div>");
            sb.Append("<script>window.onload=function(){window.print();}</script></body></html>");

            OpenHtml(sb.ToString(), slipNo);
        }

        // ================= HELPERS =================
        private void OpenHtml(string html, string name)
        {
            try
            {
                var path = Path.Combine(Path.GetTempPath(), $"{name}.html");
                File.WriteAllText(path, html, Encoding.UTF8);
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                Show(InfoBarSeverity.Informational, "Opened in your browser. Print it, or choose \"Save as PDF\".");
            }
            catch (Exception ex)
            {
                Show(InfoBarSeverity.Error, "Could not open the print page: " + ex.Message);
            }
        }

        private void Row_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Grid g && g.Children.OfType<Button>().LastOrDefault() is Button b) b.Opacity = 1;
        }

        private void Row_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Grid g && g.Children.OfType<Button>().LastOrDefault() is Button b) b.Opacity = 0.3;
        }

        private async Task<bool> ConfirmAsync(string title, string message, string primary)
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = message,
                PrimaryButtonText = primary,
                CloseButtonText = "Back",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot
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