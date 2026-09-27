using System;
using System.Collections.Generic;
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
using Microsoft.UI.Xaml.Media;

namespace Anorath.WinUI.Pages
{
    // ----- API shapes -----
    public class ResReportRow
    {
        public int ReservationId { get; set; }
        public string CustomerName { get; set; } = "";
        public string RoomNumber { get; set; } = "";
        public string RoomType { get; set; } = "";
        public DateTime CheckIn { get; set; }
        public DateTime CheckOut { get; set; }
        public int Nights { get; set; }
        public decimal Amount { get; set; }
        public string Status { get; set; } = "";
    }
    public class ResReport
    {
        public List<ResReportRow> Rows { get; set; } = new();
        public int TotalReservations { get; set; }
        public int EarningReservations { get; set; }
        public decimal TotalRevenue { get; set; }
    }
    public class RestReportRow
    {
        public string ItemName { get; set; } = "";
        public int QuantitySold { get; set; }
        public int Transactions { get; set; }
        public decimal TotalSales { get; set; }
    }
    public class PayReportRow
    {
        public string PaymentMethod { get; set; } = "";
        public decimal Amount { get; set; }
    }
    public class RestReport
    {
        public List<RestReportRow> Rows { get; set; } = new();
        public List<PayReportRow> ByPayment { get; set; } = new();
        public int TotalTransactions { get; set; }
        public int TotalItemsSold { get; set; }
        public decimal TotalSales { get; set; }
    }

    // One table on the report (drawn on screen, in the PDF and in the CSV from the same data)
    internal class ReportTable
    {
        public string Title { get; set; } = "";
        public string[] Headers { get; set; } = Array.Empty<string>();
        public double[] Widths { get; set; } = Array.Empty<double>();   // 0 = fill
        public HashSet<int> Right { get; set; } = new();
        public List<string[]> Rows { get; set; } = new();
        public string[]? Total { get; set; }
    }

    public sealed partial class ReportsPage : Page
    {
        private static readonly string[] Reports = { "Reservations Report", "Restaurant Sales Report" };
        private static readonly string[] Presets = { "This month", "Last month", "This quarter", "This year", "Custom" };

        private readonly List<(string Label, string Value)> _summary = new();
        private readonly List<ReportTable> _tables = new();
        private string _title = "";
        private DateTime _from, _to;

        public ReportsPage()
        {
            InitializeComponent();
            ReportBox.ItemsSource = Reports;
            PresetBox.ItemsSource = Presets;
            ReportBox.SelectedIndex = 0;
            PresetBox.SelectedIndex = 0;
            Loaded += async (s, e) => await GenerateAsync();
        }

        private async void ReportBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded) await GenerateAsync();
        }

        private async void PresetBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
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
                default: return;
            }
            FromPicker.Date = from;
            ToPicker.Date = to;
            if (IsLoaded) await GenerateAsync();
        }

        private async void Generate_Click(object sender, RoutedEventArgs e) => await GenerateAsync();

        private async Task GenerateAsync()
        {
            if (FromPicker.Date is null || ToPicker.Date is null)
            {
                Show(InfoBarSeverity.Warning, "Choose both the From and To dates.");
                return;
            }
            _from = FromPicker.Date.Value.Date;
            _to = ToPicker.Date.Value.Date;
            if (_to < _from)
            {
                Show(InfoBarSeverity.Warning, "'To' date must be on or after 'From' date.");
                return;
            }

            var range = $"from={_from:yyyy-MM-dd}&to={_to:yyyy-MM-dd}";
            _summary.Clear();
            _tables.Clear();

            try
            {
                if (ReportBox.SelectedIndex == 0)
                {
                    _title = "Reservations Report";
                    var r = await ApiClient.GetAsync<ResReport>("reports/reservations?" + range);

                    _summary.Add(("Total reservations", r.TotalReservations.ToString("N0")));
                    _summary.Add(("Confirmed / stayed", r.EarningReservations.ToString("N0")));
                    _summary.Add(("Room revenue", Money(r.TotalRevenue)));

                    _tables.Add(new ReportTable
                    {
                        Title = "Reservations by check-in date",
                        Headers = new[] { "Res. No", "Guest", "Room", "Type", "Check-in", "Check-out", "Nights", "Amount", "Status" },
                        Widths = new double[] { 90, 0, 60, 110, 100, 100, 60, 110, 95 },
                        Right = new HashSet<int> { 6, 7 },
                        Rows = r.Rows.Select(x => new[]
                        {
                            $"RES-{x.ReservationId:00000}", x.CustomerName, x.RoomNumber, x.RoomType,
                            x.CheckIn.ToString("MMM dd, yyyy"), x.CheckOut.ToString("MMM dd, yyyy"),
                            x.Nights.ToString(), Money(x.Amount), x.Status
                        }).ToList(),
                        Total = new[] { "", "Revenue (excludes Pending & Cancelled)", "", "", "", "", "", Money(r.TotalRevenue), "" }
                    });
                }
                else
                {
                    _title = "Restaurant Sales Report";
                    var r = await ApiClient.GetAsync<RestReport>("reports/restaurant?" + range);

                    _summary.Add(("Transactions", r.TotalTransactions.ToString("N0")));
                    _summary.Add(("Items sold", r.TotalItemsSold.ToString("N0")));
                    _summary.Add(("Total sales", Money(r.TotalSales)));

                    _tables.Add(new ReportTable
                    {
                        Title = "Sales by menu item",
                        Headers = new[] { "Menu item", "Qty sold", "Transactions", "Sales" },
                        Widths = new double[] { 0, 110, 120, 140 },
                        Right = new HashSet<int> { 1, 2, 3 },
                        Rows = r.Rows.Select(x => new[] { x.ItemName, x.QuantitySold.ToString("N0"), x.Transactions.ToString("N0"), Money(x.TotalSales) }).ToList(),
                        Total = new[] { "TOTAL", r.TotalItemsSold.ToString("N0"), r.TotalTransactions.ToString("N0"), Money(r.TotalSales) }
                    });

                    _tables.Add(new ReportTable
                    {
                        Title = "Sales by payment method",
                        Headers = new[] { "Payment method", "Amount" },
                        Widths = new double[] { 0, 160 },
                        Right = new HashSet<int> { 1 },
                        Rows = r.ByPayment.Select(x => new[] { x.PaymentMethod, Money(x.Amount) }).ToList(),
                        Total = new[] { "TOTAL", Money(r.TotalSales) }
                    });
                }

                Render();
                PrintButton.IsEnabled = ExportButton.IsEnabled = true;
                MessageBar.IsOpen = false;
            }
            catch (Exception ex)
            {
                SummaryPanel.Children.Clear();
                TablesPanel.Children.Clear();
                PrintButton.IsEnabled = ExportButton.IsEnabled = false;
                Show(InfoBarSeverity.Error, ex.Message);
            }
        }

        // ---------- On-screen ----------
        private void Render()
        {
            SummaryPanel.Children.Clear();
            foreach (var (label, value) in _summary)
            {
                var card = new StackPanel { Spacing = 4 };
                card.Children.Add(new TextBlock { Text = label, Opacity = 0.7 });
                card.Children.Add(new TextBlock { Text = value, FontSize = 24, FontWeight = FontWeights.SemiBold });
                SummaryPanel.Children.Add(new Border
                {
                    Width = 220,
                    Padding = new Thickness(16),
                    CornerRadius = new CornerRadius(8),
                    BorderThickness = new Thickness(1),
                    BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(60, 128, 128, 128)),
                    Child = card
                });
            }

            TablesPanel.Children.Clear();
            foreach (var t in _tables)
            {
                var block = new StackPanel { Spacing = 2 };
                block.Children.Add(new TextBlock { Text = t.Title, FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) });
                block.Children.Add(Row(t, t.Headers, bold: true));
                block.Children.Add(Divider());

                if (t.Rows.Count == 0)
                    block.Children.Add(new TextBlock { Text = "No records for this period.", Opacity = 0.6, Margin = new Thickness(12, 8, 0, 8) });
                foreach (var r in t.Rows) block.Children.Add(Row(t, r));

                if (t.Total != null && t.Rows.Count > 0)
                {
                    block.Children.Add(Divider());
                    block.Children.Add(Row(t, t.Total, bold: true));
                }
                TablesPanel.Children.Add(block);
            }
        }

        private static Grid Row(ReportTable t, string[] cells, bool bold = false)
        {
            var g = new Grid { ColumnSpacing = 8, Padding = new Thickness(12, 5, 12, 5) };
            for (var i = 0; i < t.Headers.Length; i++)
            {
                g.ColumnDefinitions.Add(new ColumnDefinition
                {
                    Width = t.Widths[i] == 0 ? new GridLength(1, GridUnitType.Star) : new GridLength(t.Widths[i])
                });
                var tb = new TextBlock
                {
                    Text = i < cells.Length ? cells[i] : "",
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    HorizontalAlignment = t.Right.Contains(i) ? HorizontalAlignment.Right : HorizontalAlignment.Left
                };
                if (bold) tb.FontWeight = FontWeights.SemiBold;
                Grid.SetColumn(tb, i);
                g.Children.Add(tb);
            }
            return g;
        }

        private static Border Divider() => new Border
        {
            Height = 1,
            Background = new SolidColorBrush(ColorHelper.FromArgb(70, 128, 128, 128))
        };

        // ---------- Print / PDF ----------
        private void Print_Click(object sender, RoutedEventArgs e)
        {
            static string H(string? s) => WebUtility.HtmlEncode(s ?? "");
            var sb = new StringBuilder();
            sb.Append("<!doctype html><html><head><meta charset='utf-8'><title>").Append(H(_title)).Append("</title><style>");
            sb.Append(@"
body{font-family:'Segoe UI',Arial,sans-serif;color:#222;margin:32px;font-size:12px;}
.top{border-bottom:3px solid #1f4e79;padding-bottom:8px;display:flex;justify-content:space-between;align-items:flex-end;}
.company{font-size:20px;font-weight:700;color:#1f4e79;} .title{font-size:16px;font-weight:700;text-align:right;}
.cards{display:flex;gap:12px;margin:18px 0;} .card{border:1px solid #ccd;border-radius:6px;padding:10px 14px;min-width:150px;}
.card .l{color:#666;} .card .v{font-size:18px;font-weight:700;}
h3{font-size:14px;margin:22px 0 6px;color:#1f4e79;}
table{width:100%;border-collapse:collapse;} th{background:#1f4e79;color:#fff;text-align:left;padding:6px;}
td{padding:6px;border-bottom:1px solid #e3e3e3;} .r{text-align:right;} tr.t td{font-weight:700;border-top:2px solid #333;}
.small{color:#666;font-size:11px;margin-top:24px;}
@media print{body{margin:12mm;}}
");
            sb.Append("</style></head><body>");
            sb.Append("<div class='top'><div class='company'>").Append(H(Session.CompanyName)).Append("</div><div class='title'>")
              .Append(H(_title).ToUpperInvariant()).Append("<br><span style='font-weight:400;font-size:12px'>")
              .Append(_from.ToString("MMM dd, yyyy")).Append(" – ").Append(_to.ToString("MMM dd, yyyy")).Append("</span></div></div>");

            sb.Append("<div class='cards'>");
            foreach (var (l, v) in _summary)
                sb.Append("<div class='card'><div class='l'>").Append(H(l)).Append("</div><div class='v'>").Append(H(v)).Append("</div></div>");
            sb.Append("</div>");

            foreach (var t in _tables)
            {
                sb.Append("<h3>").Append(H(t.Title)).Append("</h3><table><tr>");
                for (var i = 0; i < t.Headers.Length; i++)
                    sb.Append("<th").Append(t.Right.Contains(i) ? " class='r'" : "").Append(">").Append(H(t.Headers[i])).Append("</th>");
                sb.Append("</tr>");

                if (t.Rows.Count == 0)
                    sb.Append("<tr><td colspan='").Append(t.Headers.Length).Append("'>No records for this period.</td></tr>");
                foreach (var r in t.Rows) AppendRow(sb, t, r, "");
                if (t.Total != null && t.Rows.Count > 0) AppendRow(sb, t, t.Total, "t");
                sb.Append("</table>");
            }

            sb.Append("<div class='small'>Generated ").Append(DateTime.Now.ToString("MMM dd, yyyy h:mm tt"))
              .Append(" by ").Append(H(Session.FullName)).Append(" (").Append(H(Session.Role)).Append(")</div>");
            sb.Append("<script>window.onload=function(){window.print();}</script></body></html>");

            try
            {
                var path = Path.Combine(Path.GetTempPath(), $"{_title.Replace(" ", "")}_{_from:yyyyMMdd}_{_to:yyyyMMdd}.html");
                File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                Show(InfoBarSeverity.Informational, "Opened in your browser. Print it, or choose \"Save as PDF\".");
            }
            catch (Exception ex)
            {
                Show(InfoBarSeverity.Error, "Could not open the print page: " + ex.Message);
            }
        }

        private static void AppendRow(StringBuilder sb, ReportTable t, string[] cells, string cls)
        {
            sb.Append("<tr").Append(cls.Length > 0 ? $" class='{cls}'" : "").Append(">");
            for (var i = 0; i < t.Headers.Length; i++)
                sb.Append("<td").Append(t.Right.Contains(i) ? " class='r'" : "").Append(">")
                  .Append(WebUtility.HtmlEncode(i < cells.Length ? cells[i] : "")).Append("</td>");
            sb.Append("</tr>");
        }

        // ---------- Export CSV (opens in Excel) ----------
        private void Export_Click(object sender, RoutedEventArgs e)
        {
            static string C(string? v)
            {
                v ??= "";
                return v.Contains(',') || v.Contains('"') || v.Contains('\n') ? "\"" + v.Replace("\"", "\"\"") + "\"" : v;
            }

            var sb = new StringBuilder();
            sb.AppendLine(C(Session.CompanyName));
            sb.AppendLine(C(_title));
            sb.AppendLine(C($"Period: {_from:MMM dd, yyyy} - {_to:MMM dd, yyyy}"));
            sb.AppendLine();
            foreach (var (l, v) in _summary) sb.AppendLine($"{C(l)},{C(v)}");

            foreach (var t in _tables)
            {
                sb.AppendLine();
                sb.AppendLine(C(t.Title));
                sb.AppendLine(string.Join(",", t.Headers.Select(C)));
                foreach (var r in t.Rows) sb.AppendLine(string.Join(",", r.Select(C)));
                if (t.Total != null && t.Rows.Count > 0) sb.AppendLine(string.Join(",", t.Total.Select(C)));
            }

            try
            {
                var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                Directory.CreateDirectory(folder);
                var path = Path.Combine(folder, $"{_title.Replace(" ", "")}_{_from:yyyyMMdd}_{_to:yyyyMMdd}.csv");
                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));   // BOM so Excel shows ₱ correctly
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                Show(InfoBarSeverity.Success, $"Exported to {path}");
            }
            catch (Exception ex)
            {
                Show(InfoBarSeverity.Error, "Export failed (is the file open in Excel?): " + ex.Message);
            }
        }

        private static string Money(decimal v) => "₱" + v.ToString("N2");

        private void Show(InfoBarSeverity severity, string message)
        {
            MessageBar.Severity = severity;
            MessageBar.Message = message;
            MessageBar.IsOpen = true;
        }
    }
}