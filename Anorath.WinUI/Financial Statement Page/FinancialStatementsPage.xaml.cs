using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Anorath.WinUI.Services;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Anorath.WinUI.Pages
{
    public class AccountLine
    {
        public string Account { get; set; } = "";
        public decimal Amount { get; set; }
    }

    public class IncomeStatementDto
    {
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public List<AccountLine> RevenueLines { get; set; } = new();
        public decimal TotalRevenue { get; set; }
        public List<AccountLine> ExpenseLines { get; set; } = new();
        public decimal TotalExpenses { get; set; }
        public decimal IncomeBeforeTax { get; set; }
        public decimal TaxRate { get; set; }
        public decimal IncomeTax { get; set; }
        public decimal NetIncome { get; set; }
    }

    public sealed partial class FinancialStatementsPage : Page
    {
        private static readonly string[] Presets = { "This month", "Last month", "This quarter", "This year", "Custom" };
        private static readonly string[] TaxOptions = { "Regular corporation – 25%", "Small corporation – 20%" };

        private IncomeStatementDto? _current;

        private const string TaxNote =
            "Income tax is computed under the CREATE Act (RA 11534): 25% regular corporate income tax, or 20% for " +
            "corporations with net taxable income of ₱5M or less and total assets of ₱100M or less (excluding land). " +
            "No tax is due on a net loss. Revenue counts Confirmed, Checked-in and Checked-out reservations plus " +
            "restaurant sales; expenses count Received purchase orders and payroll.";

        public FinancialStatementsPage()
        {
            InitializeComponent();
            PresetBox.ItemsSource = Presets;
            TaxBox.ItemsSource = TaxOptions;
            TaxBox.SelectedIndex = 0;
            PresetBox.SelectedIndex = 0;   // also fills From/To
            Loaded += async (s, e) => await GenerateAsync();
        }

        // ---------- Period presets ----------
        private void PresetBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FromPicker is null || ToPicker is null) return;

            var today = DateTime.Today;
            DateTime from, to;
            switch (PresetBox.SelectedIndex)
            {
                case 0: from = new DateTime(today.Year, today.Month, 1); to = from.AddMonths(1).AddDays(-1); break;
                case 1: from = new DateTime(today.Year, today.Month, 1).AddMonths(-1); to = from.AddMonths(1).AddDays(-1); break;
                case 2: var qm = (today.Month - 1) / 3 * 3 + 1; from = new DateTime(today.Year, qm, 1); to = from.AddMonths(3).AddDays(-1); break;
                case 3: from = new DateTime(today.Year, 1, 1); to = new DateTime(today.Year, 12, 31); break;
                default: return;   // Custom: user picks the dates
            }
            FromPicker.Date = from;
            ToPicker.Date = to;
        }

        private async void Generate_Click(object sender, RoutedEventArgs e) => await GenerateAsync();

        private async Task GenerateAsync()
        {
            if (FromPicker.Date is null || ToPicker.Date is null)
            {
                Show(InfoBarSeverity.Warning, "Choose both the From and To dates.");
                return;
            }
            var from = FromPicker.Date.Value.Date;
            var to = ToPicker.Date.Value.Date;
            if (to < from)
            {
                Show(InfoBarSeverity.Warning, "'To' date must be on or after 'From' date.");
                return;
            }

            var rate = TaxBox.SelectedIndex == 1 ? 0.20m : 0.25m;
            var url = $"financials/income-statement?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}" +
                      $"&taxRate={rate.ToString(CultureInfo.InvariantCulture)}";

            try
            {
                _current = await ApiClient.GetAsync<IncomeStatementDto>(url);
                Render(_current);
                PrintButton.IsEnabled = true;
                MessageBar.IsOpen = false;
            }
            catch (Exception ex)
            {
                _current = null;
                StatementPanel.Children.Clear();
                PrintButton.IsEnabled = false;
                Show(InfoBarSeverity.Error, ex.Message);
            }
        }

        // ---------- Draw the statement ----------
        private void Render(IncomeStatementDto s)
        {
            var p = StatementPanel;
            p.Children.Clear();

            p.Children.Add(Center(Session.CompanyName, 20, true));
            p.Children.Add(Center("INCOME STATEMENT", 16, true));
            p.Children.Add(Center($"For the period {s.PeriodStart:MMMM dd, yyyy} to {s.PeriodEnd:MMMM dd, yyyy}", 13, false, 0.75));
            p.Children.Add(new Border { Height = 20 });

            Heading("REVENUE");
            foreach (var l in s.RevenueLines) Row(l.Account, l.Amount, indent: 24);
            Line();
            Row("Total Revenue", s.TotalRevenue, bold: true);
            p.Children.Add(new Border { Height = 14 });

            Heading("OPERATING EXPENSES");
            foreach (var l in s.ExpenseLines) Row(l.Account, l.Amount, indent: 24);
            Line();
            Row("Total Operating Expenses", s.TotalExpenses, bold: true);
            p.Children.Add(new Border { Height = 14 });

            Row(s.IncomeBeforeTax >= 0 ? "INCOME BEFORE TAX" : "LOSS BEFORE TAX", s.IncomeBeforeTax, bold: true);
            Row(s.IncomeBeforeTax > 0
                    ? $"Less: Income Tax ({s.TaxRate:P0})"
                    : "Less: Income Tax (none — net loss)",
                -s.IncomeTax, indent: 24);
            Line(2);
            Row(s.NetIncome >= 0 ? "NET INCOME" : "NET LOSS", s.NetIncome, bold: true, size: 18,
                color: s.NetIncome >= 0 ? Colors.SeaGreen : Colors.Firebrick);
            Line(2);

            p.Children.Add(new Border { Height = 16 });
            p.Children.Add(new TextBlock { Text = TaxNote, FontSize = 11, Opacity = 0.65, TextWrapping = TextWrapping.Wrap });
            p.Children.Add(new TextBlock
            {
                Text = $"Generated {DateTime.Now:MMM dd, yyyy h:mm tt} by {Session.FullName}",
                FontSize = 11,
                Opacity = 0.65,
                Margin = new Thickness(0, 6, 0, 0)
            });
        }

        private static string Money(decimal v) => v < 0 ? $"(₱{-v:N2})" : $"₱{v:N2}";

        private static TextBlock Center(string text, double size, bool bold, double opacity = 1)
        {
            var t = new TextBlock { Text = text, FontSize = size, Opacity = opacity, HorizontalAlignment = HorizontalAlignment.Center };
            if (bold) t.FontWeight = FontWeights.SemiBold;
            return t;
        }

        private void Heading(string text) =>
            StatementPanel.Children.Add(new TextBlock { Text = text, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 4, 0, 4) });

        private void Line(double thickness = 1) =>
            StatementPanel.Children.Add(new Border
            {
                Height = thickness,
                Background = new SolidColorBrush(Colors.Gray),
                HorizontalAlignment = HorizontalAlignment.Right,
                Width = 180,
                Margin = new Thickness(0, 2, 0, 2)
            });

        private void Row(string label, decimal amount, bool bold = false, double indent = 0, double size = 14, Windows.UI.Color? color = null)
        {
            var g = new Grid { Padding = new Thickness(indent, 3, 0, 3) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });

            var l = new TextBlock { Text = label, FontSize = size };
            var a = new TextBlock { Text = Money(amount), FontSize = size, HorizontalAlignment = HorizontalAlignment.Right };
            if (bold) { l.FontWeight = FontWeights.SemiBold; a.FontWeight = FontWeights.SemiBold; }
            if (color != null) a.Foreground = new SolidColorBrush(color.Value);

            Grid.SetColumn(a, 1);
            g.Children.Add(l);
            g.Children.Add(a);
            StatementPanel.Children.Add(g);
        }

        // ---------- Print / Save as PDF ----------
        private void Print_Click(object sender, RoutedEventArgs e)
        {
            if (_current == null) return;
            var s = _current;
            static string H(string? t) => WebUtility.HtmlEncode(t ?? "");

            var sb = new StringBuilder();
            sb.Append("<!doctype html><html><head><meta charset='utf-8'><title>Income Statement</title><style>");
            sb.Append(@"
body{font-family:'Segoe UI',Arial,sans-serif;color:#222;margin:48px auto;max-width:720px;}
h1,h2,.sub{text-align:center;margin:0;}
h1{font-size:22px;color:#1f4e79;} h2{font-size:17px;margin-top:4px;} .sub{font-size:13px;color:#555;margin-top:4px;}
table{width:100%;border-collapse:collapse;margin-top:28px;font-size:14px;}
td{padding:5px 0;} td.a{text-align:right;width:180px;}
.head td{font-weight:700;padding-top:16px;}
.ind td:first-child{padding-left:24px;}
.tot td{font-weight:700;} .tot td.a{border-top:1px solid #333;}
.net td{font-weight:700;font-size:17px;} .net td.a{border-top:2px solid #333;border-bottom:4px double #333;}
.note{font-size:11px;color:#666;margin-top:32px;line-height:1.5;}
@media print{body{margin:15mm auto;}}
");
            sb.Append("</style></head><body>");
            sb.Append("<h1>").Append(H(Session.CompanyName)).Append("</h1><h2>INCOME STATEMENT</h2>");
            sb.Append("<div class='sub'>For the period ").Append(s.PeriodStart.ToString("MMMM dd, yyyy"))
              .Append(" to ").Append(s.PeriodEnd.ToString("MMMM dd, yyyy")).Append("</div><table>");

            void Tr(string cls, string label, string amount) =>
                sb.Append("<tr class='").Append(cls).Append("'><td>").Append(H(label)).Append("</td><td class='a'>").Append(amount).Append("</td></tr>");

            Tr("head", "REVENUE", "");
            foreach (var l in s.RevenueLines) Tr("ind", l.Account, Money(l.Amount));
            Tr("tot", "Total Revenue", Money(s.TotalRevenue));

            Tr("head", "OPERATING EXPENSES", "");
            foreach (var l in s.ExpenseLines) Tr("ind", l.Account, Money(l.Amount));
            Tr("tot", "Total Operating Expenses", Money(s.TotalExpenses));

            Tr("head", s.IncomeBeforeTax >= 0 ? "INCOME BEFORE TAX" : "LOSS BEFORE TAX", Money(s.IncomeBeforeTax));
            Tr("ind", s.IncomeBeforeTax > 0 ? $"Less: Income Tax ({s.TaxRate:P0})" : "Less: Income Tax (none — net loss)", Money(-s.IncomeTax));
            Tr("net", s.NetIncome >= 0 ? "NET INCOME" : "NET LOSS", Money(s.NetIncome));
            sb.Append("</table>");

            sb.Append("<div class='note'>").Append(H(TaxNote)).Append("<br>Generated ")
              .Append(DateTime.Now.ToString("MMM dd, yyyy h:mm tt")).Append(" by ").Append(H(Session.FullName)).Append("</div>");
            sb.Append("<script>window.onload=function(){window.print();}</script></body></html>");

            try
            {
                var path = Path.Combine(Path.GetTempPath(), $"IncomeStatement_{s.PeriodStart:yyyyMMdd}_{s.PeriodEnd:yyyyMMdd}.html");
                File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                Show(InfoBarSeverity.Informational, "Opened in your browser. In the print window choose \"Save as PDF\".");
            }
            catch (Exception ex)
            {
                Show(InfoBarSeverity.Error, "Could not open the print page: " + ex.Message);
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