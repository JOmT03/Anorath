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
    // One purchase order in the list (Model)
    public class PoRow
    {
        public int PurchaseOrderId { get; set; }
        public string PoNumber { get; set; } = "";
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = "";
        public DateTime OrderDate { get; set; }
        public string ItemDescription { get; set; } = "";
        public int Quantity { get; set; }
        public decimal TotalCost { get; set; }
        public string Status { get; set; } = "";
        public DateTime? ReceivedDate { get; set; }

        public string DateText => OrderDate.ToString("MMM dd, yyyy");
        public string QtyText => Quantity.ToString("N0");
        public string TotalText => "₱" + TotalCost.ToString("N2");
        public bool CanChange => Status == "Pending";
        public bool CanDelete => Status != "Received";
        public SolidColorBrush StatusBrush => new(Status switch
        {
            "Received" => Colors.SeaGreen,
            "Cancelled" => Colors.Gray,
            _ => Colors.DarkOrange
        });
    }

    // One line of a saved PO (used for View / Print)
    public class PoLineRow
    {
        public string ItemName { get; set; } = "";
        public string Unit { get; set; } = "";
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
    }

    // Full PO with supplier + lines (GET purchaseorders/{id})
    public class PoDetail
    {
        public int PurchaseOrderId { get; set; }
        public string PoNumber { get; set; } = "";
        public DateTime OrderDate { get; set; }
        public string Status { get; set; } = "";
        public DateTime? ReceivedDate { get; set; }
        public decimal TotalCost { get; set; }
        public SupplierRow? Supplier { get; set; }
        public List<PoLineRow> Lines { get; set; } = new();
    }

    public class PoCreated
    {
        public int PurchaseOrderId { get; set; }
        public string PoNumber { get; set; } = "";
        public decimal TotalCost { get; set; }
    }

    // A line being built inside the New PO dialog
    internal class PoDraftLine
    {
        public SupplierItemRow Item { get; set; } = new();
        public int Quantity { get; set; }
        public decimal LineTotal => Item.UnitPrice * Quantity;
    }

    public sealed partial class PurchaseOrdersPage : Page
    {
        private static readonly string[] Statuses = { "All", "Pending", "Received", "Cancelled" };

        private readonly ObservableCollection<PoRow> _orders = new();
        private List<SupplierRow> _suppliers = new();
        private bool _ready;

        public PurchaseOrdersPage()
        {
            InitializeComponent();
            OrdersList.ItemsSource = _orders;
            StatusFilter.ItemsSource = Statuses;
            StatusFilter.SelectedIndex = 0;
            Loaded += async (s, e) => await RefreshAsync();
        }

        // Called on load and when the Purchase Orders tab is opened
        public async Task RefreshAsync()
        {
            _ready = false;
            await LoadSuppliersAsync();
            _ready = true;
            await LoadOrdersAsync();
        }

        private async Task LoadSuppliersAsync()
        {
            try
            {
                var keepId = (SupplierFilter.SelectedItem as SupplierRow)?.SupplierId ?? 0;
                _suppliers = await ApiClient.GetAsync<List<SupplierRow>>("suppliers");

                var options = new List<SupplierRow> { new SupplierRow { SupplierId = 0, SupplierName = "All suppliers" } };
                options.AddRange(_suppliers);
                SupplierFilter.ItemsSource = options;
                SupplierFilter.SelectedItem = options.FirstOrDefault(s => s.SupplierId == keepId) ?? options[0];
            }
            catch (Exception ex)
            {
                Show(InfoBarSeverity.Error, ex.Message);
            }
        }

        private async Task LoadOrdersAsync()
        {
            try
            {
                var supplierId = (SupplierFilter.SelectedItem as SupplierRow)?.SupplierId ?? 0;
                var status = StatusFilter.SelectedItem as string ?? "All";

                var url = $"purchaseorders?status={Uri.EscapeDataString(status)}";
                if (supplierId > 0) url += $"&supplierId={supplierId}";

                var list = await ApiClient.GetAsync<List<PoRow>>(url);
                _orders.Clear();
                foreach (var o in list) _orders.Add(o);

                EmptyText.Visibility = _orders.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

                var pending = list.Where(o => o.Status == "Pending").Sum(o => o.TotalCost);
                var received = list.Where(o => o.Status == "Received").Sum(o => o.TotalCost);
                SummaryText.Text = $"{list.Count} order(s)   ·   Pending: ₱{pending:N2}   ·   Received (expense): ₱{received:N2}";
            }
            catch (Exception ex)
            {
                Show(InfoBarSeverity.Error, ex.Message);
            }
        }

        private async void Filter_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_ready) await LoadOrdersAsync();
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

        // ================= NEW PURCHASE ORDER =================
        private async void NewPo_Click(object sender, RoutedEventArgs e)
        {
            var active = _suppliers.Where(s => s.IsActive).ToList();
            if (active.Count == 0)
            {
                Show(InfoBarSeverity.Warning, "Add an active supplier first (Suppliers tab).");
                return;
            }

            var lines = new List<PoDraftLine>();

            // --- controls ---
            var supplierBox = new ComboBox { Header = "Supplier", ItemsSource = active, DisplayMemberPath = "SupplierName", PlaceholderText = "Choose supplier", HorizontalAlignment = HorizontalAlignment.Stretch };
            var datePicker = new CalendarDatePicker { Header = "Order date", Date = DateTimeOffset.Now, MaxDate = DateTimeOffset.Now, HorizontalAlignment = HorizontalAlignment.Stretch };
            var itemBox = new ComboBox { Header = "Item", DisplayMemberPath = "ItemName", PlaceholderText = "Choose supplier first", IsEnabled = false, HorizontalAlignment = HorizontalAlignment.Stretch };
            var priceBox = new TextBox { Header = "Unit price", IsReadOnly = true, Text = "₱0.00" };
            var qtyBox = new NumberBox { Header = "Qty", Value = 1, Minimum = 1, Maximum = 100000, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
            var lineTotalBox = new TextBox { Header = "Line total", IsReadOnly = true, Text = "₱0.00" };
            var addLineButton = new Button { Content = "Add", IsEnabled = false, VerticalAlignment = VerticalAlignment.Bottom };
            var linesPanel = new StackPanel { Spacing = 6 };
            var totalText = new TextBlock { Text = "Total: ₱0.00", FontSize = 18, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Right };
            var errorText = new TextBlock { Foreground = new SolidColorBrush(Colors.Firebrick), TextWrapping = TextWrapping.Wrap };

            // --- helpers ---
            void UpdateLinePreview()
            {
                if (itemBox.SelectedItem is SupplierItemRow it)
                {
                    var q = double.IsNaN(qtyBox.Value) ? 0 : (int)qtyBox.Value;
                    priceBox.Text = $"₱{it.UnitPrice:N2} / {it.Unit}";
                    lineTotalBox.Text = $"₱{it.UnitPrice * q:N2}";
                    addLineButton.IsEnabled = q > 0;
                }
                else
                {
                    priceBox.Text = "₱0.00";
                    lineTotalBox.Text = "₱0.00";
                    addLineButton.IsEnabled = false;
                }
            }

            void RenderLines()
            {
                linesPanel.Children.Clear();
                if (lines.Count == 0)
                    linesPanel.Children.Add(new TextBlock { Text = "No items yet. Pick an item, set the quantity, then click Add.", Opacity = 0.6 });

                foreach (var line in lines.ToList())
                {
                    var row = NewLineGrid();
                    AddCell(row, 0, line.Item.ItemName);
                    AddCell(row, 1, $"{line.Quantity} {line.Item.Unit}");
                    AddCell(row, 2, $"₱{line.Item.UnitPrice:N2}");
                    AddCell(row, 3, $"₱{line.LineTotal:N2}");

                    var remove = new Button { Content = "✕", Padding = new Thickness(8, 2, 8, 2) };
                    remove.Click += (s, a) => { lines.Remove(line); RenderLines(); };
                    Grid.SetColumn(remove, 4);
                    row.Children.Add(remove);

                    linesPanel.Children.Add(row);
                }
                totalText.Text = $"Total: ₱{lines.Sum(l => l.LineTotal):N2}";
            }

            // --- events ---
            supplierBox.SelectionChanged += async (s, a) =>
            {
                lines.Clear();
                RenderLines();
                itemBox.ItemsSource = null;
                itemBox.IsEnabled = false;
                UpdateLinePreview();

                if (supplierBox.SelectedItem is not SupplierRow sup) return;
                try
                {
                    var items = await ApiClient.GetAsync<List<SupplierItemRow>>($"suppliers/{sup.SupplierId}/items?activeOnly=true");
                    itemBox.ItemsSource = items;
                    itemBox.IsEnabled = items.Count > 0;
                    itemBox.PlaceholderText = items.Count > 0 ? "Choose item" : "This supplier has no active items";
                    errorText.Text = "";
                }
                catch (Exception ex)
                {
                    errorText.Text = ex.Message;
                }
            };

            itemBox.SelectionChanged += (s, a) => UpdateLinePreview();
            qtyBox.ValueChanged += (s, a) => UpdateLinePreview();

            addLineButton.Click += (s, a) =>
            {
                if (itemBox.SelectedItem is not SupplierItemRow it) return;
                var q = double.IsNaN(qtyBox.Value) ? 0 : (int)qtyBox.Value;
                if (q <= 0) { errorText.Text = "Quantity must be at least 1."; return; }

                // Same item added twice → just add the quantity
                var existing = lines.FirstOrDefault(l => l.Item.SupplierItemId == it.SupplierItemId);
                if (existing != null) existing.Quantity += q;
                else lines.Add(new PoDraftLine { Item = it, Quantity = q });

                errorText.Text = "";
                itemBox.SelectedIndex = -1;
                qtyBox.Value = 1;
                RenderLines();
            };

            // --- layout ---
            var top = new Grid { ColumnSpacing = 12 };
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
            top.Children.Add(supplierBox);
            Grid.SetColumn(datePicker, 1);
            top.Children.Add(datePicker);

            var entry = new Grid { ColumnSpacing = 8 };
            foreach (var w in new[] { new GridLength(1, GridUnitType.Star), new GridLength(140), new GridLength(100), new GridLength(120), GridLength.Auto })
                entry.ColumnDefinitions.Add(new ColumnDefinition { Width = w });
            entry.Children.Add(itemBox);
            Grid.SetColumn(priceBox, 1); entry.Children.Add(priceBox);
            Grid.SetColumn(qtyBox, 2); entry.Children.Add(qtyBox);
            Grid.SetColumn(lineTotalBox, 3); entry.Children.Add(lineTotalBox);
            Grid.SetColumn(addLineButton, 4); entry.Children.Add(addLineButton);

            var header = NewLineGrid();
            AddCell(header, 0, "Item", true);
            AddCell(header, 1, "Qty", true);
            AddCell(header, 2, "Unit price", true);
            AddCell(header, 3, "Line total", true);

            var linesBorder = new Border
            {
                BorderBrush = new SolidColorBrush(Colors.LightGray),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10),
                Child = new StackPanel { Spacing = 6, Children = { header, linesPanel } }
            };

            var form = new StackPanel { Spacing = 12, Width = 660 };
            form.Children.Add(top);
            form.Children.Add(new TextBlock { Text = "Add items", FontWeight = FontWeights.SemiBold });
            form.Children.Add(entry);
            form.Children.Add(linesBorder);
            form.Children.Add(totalText);
            form.Children.Add(errorText);
            RenderLines();

            PoCreated? created = null;
            var dialog = new ContentDialog
            {
                Title = "New Purchase Order",
                Content = new ScrollViewer { Content = form, MaxHeight = 560 },
                PrimaryButtonText = "Create PO",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot
            };
            dialog.Resources["ContentDialogMaxWidth"] = 760.0;

            dialog.PrimaryButtonClick += async (s, args) =>
            {
                var deferral = args.GetDeferral();
                try
                {
                    if (supplierBox.SelectedItem is not SupplierRow sup)
                    {
                        errorText.Text = "Choose a supplier.";
                        args.Cancel = true; return;
                    }
                    if (lines.Count == 0)
                    {
                        errorText.Text = "Add at least one item.";
                        args.Cancel = true; return;
                    }
                    var date = datePicker.Date?.Date ?? DateTime.Today;
                    if (date > DateTime.Today)
                    {
                        errorText.Text = "Order date cannot be in the future.";
                        args.Cancel = true; return;
                    }

                    var body = new
                    {
                        supplierId = sup.SupplierId,
                        orderDate = date,
                        lines = lines.Select(l => new { supplierItemId = l.Item.SupplierItemId, quantity = l.Quantity }).ToList()
                    };
                    created = await ApiClient.PostAsync<PoCreated>("purchaseorders", body);
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

            if (await dialog.ShowAsync() == ContentDialogResult.Primary && created != null)
            {
                Show(InfoBarSeverity.Success, $"{created.PoNumber} created (₱{created.TotalCost:N2}). Use ⋯ → View / Print to print it.");
                await LoadOrdersAsync();
            }
        }

        // ================= VIEW / PRINT =================
        private async void View_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement { Tag: int id }) return;
            try
            {
                var po = await ApiClient.GetAsync<PoDetail>($"purchaseorders/{id}");
                var sup = po.Supplier;

                var panel = new StackPanel { Spacing = 8, Width = 600 };
                panel.Children.Add(new TextBlock { Text = $"Supplier: {sup?.SupplierName}  ({sup?.SupplierCode})", FontWeight = FontWeights.SemiBold });
                panel.Children.Add(new TextBlock { Text = $"Contact: {sup?.ContactPerson}  ·  {sup?.ContactNumber}  ·  {sup?.EmailAddress}", Opacity = 0.8, TextWrapping = TextWrapping.Wrap });
                panel.Children.Add(new TextBlock { Text = $"Order date: {po.OrderDate:MMM dd, yyyy}   ·   Status: {po.Status}" + (po.ReceivedDate != null ? $" ({po.ReceivedDate:MMM dd, yyyy})" : "") });

                var header = NewLineGrid();
                AddCell(header, 0, "Item", true);
                AddCell(header, 1, "Qty", true);
                AddCell(header, 2, "Unit price", true);
                AddCell(header, 3, "Line total", true);
                panel.Children.Add(header);

                foreach (var l in po.Lines)
                {
                    var row = NewLineGrid();
                    AddCell(row, 0, l.ItemName);
                    AddCell(row, 1, $"{l.Quantity} {l.Unit}");
                    AddCell(row, 2, $"₱{l.UnitPrice:N2}");
                    AddCell(row, 3, $"₱{l.LineTotal:N2}");
                    panel.Children.Add(row);
                }
                panel.Children.Add(new TextBlock { Text = $"Total: ₱{po.TotalCost:N2}", FontSize = 18, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Right });

                var dialog = new ContentDialog
                {
                    Title = po.PoNumber,
                    Content = new ScrollViewer { Content = panel, MaxHeight = 520 },
                    PrimaryButtonText = "Print / Save as PDF",
                    CloseButtonText = "Close",
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = XamlRoot
                };
                dialog.Resources["ContentDialogMaxWidth"] = 700.0;

                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                    PrintPo(po);
            }
            catch (Exception ex)
            {
                Show(InfoBarSeverity.Error, ex.Message);
            }
        }

        // Builds a printable HTML page and opens it in the browser → Print → "Save as PDF"
        private void PrintPo(PoDetail po)
        {
            static string H(string? s) => WebUtility.HtmlEncode(s ?? "");
            var sup = po.Supplier;
            var sb = new StringBuilder();

            sb.Append("<!doctype html><html><head><meta charset='utf-8'><title>").Append(H(po.PoNumber)).Append("</title><style>");
            sb.Append(@"
body{font-family:'Segoe UI',Arial,sans-serif;color:#222;margin:40px;}
.top{display:flex;justify-content:space-between;align-items:flex-start;border-bottom:3px solid #1f4e79;padding-bottom:12px;}
.company{font-size:22px;font-weight:700;color:#1f4e79;}
.title{font-size:26px;font-weight:700;text-align:right;}
.meta{text-align:right;font-size:13px;line-height:1.6;}
.box{margin-top:24px;font-size:13px;line-height:1.6;}
.box b{font-size:14px;}
table{width:100%;border-collapse:collapse;margin-top:24px;font-size:13px;}
th{background:#1f4e79;color:#fff;text-align:left;padding:8px;}
td{padding:8px;border-bottom:1px solid #ddd;}
.r{text-align:right;}
.total td{font-weight:700;font-size:15px;border-top:2px solid #1f4e79;border-bottom:none;}
.sign{display:flex;justify-content:space-between;margin-top:70px;font-size:13px;}
.sign div{width:40%;border-top:1px solid #333;text-align:center;padding-top:6px;}
.stamp{position:fixed;top:40%;left:20%;font-size:90px;color:rgba(200,0,0,.15);transform:rotate(-25deg);font-weight:700;}
@media print{body{margin:15mm;}}
");
            sb.Append("</style></head><body>");

            if (po.Status == "Cancelled") sb.Append("<div class='stamp'>CANCELLED</div>");

            sb.Append("<div class='top'><div><div class='company'>").Append(H(Session.CompanyName)).Append("</div>");
            sb.Append("<div style='font-size:12px;color:#666'>Powered by Anorath Resort ERP</div></div>");
            sb.Append("<div><div class='title'>PURCHASE ORDER</div><div class='meta'>");
            sb.Append("PO No: <b>").Append(H(po.PoNumber)).Append("</b><br>");
            sb.Append("Date: ").Append(po.OrderDate.ToString("MMMM dd, yyyy")).Append("<br>");
            sb.Append("Status: ").Append(H(po.Status));
            if (po.ReceivedDate != null) sb.Append(" (").Append(po.ReceivedDate.Value.ToString("MMM dd, yyyy")).Append(")");
            sb.Append("</div></div></div>");

            sb.Append("<div class='box'><b>Supplier</b><br>");
            sb.Append(H(sup?.SupplierName)).Append(" (").Append(H(sup?.SupplierCode)).Append(")<br>");
            if (!string.IsNullOrWhiteSpace(sup?.ContactPerson)) sb.Append("Attn: ").Append(H(sup.ContactPerson)).Append("<br>");
            if (!string.IsNullOrWhiteSpace(sup?.ContactNumber)) sb.Append(H(sup.ContactNumber)).Append("<br>");
            if (!string.IsNullOrWhiteSpace(sup?.EmailAddress)) sb.Append(H(sup.EmailAddress)).Append("<br>");
            if (!string.IsNullOrWhiteSpace(sup?.Address)) sb.Append(H(sup.Address));
            sb.Append("</div>");

            sb.Append("<table><tr><th>#</th><th>Item</th><th>Unit</th><th class='r'>Qty</th><th class='r'>Unit Price</th><th class='r'>Amount</th></tr>");
            var n = 1;
            foreach (var l in po.Lines)
            {
                sb.Append("<tr><td>").Append(n++).Append("</td><td>").Append(H(l.ItemName))
                  .Append("</td><td>").Append(H(l.Unit))
                  .Append("</td><td class='r'>").Append(l.Quantity.ToString("N0"))
                  .Append("</td><td class='r'>₱").Append(l.UnitPrice.ToString("N2"))
                  .Append("</td><td class='r'>₱").Append(l.LineTotal.ToString("N2")).Append("</td></tr>");
            }
            sb.Append("<tr class='total'><td colspan='5' class='r'>TOTAL</td><td class='r'>₱").Append(po.TotalCost.ToString("N2")).Append("</td></tr></table>");

            sb.Append("<div class='sign'><div>Prepared by: ").Append(H(Session.FullName)).Append("</div><div>Approved by</div></div>");
            sb.Append("<script>window.onload=function(){window.print();}</script></body></html>");

            try
            {
                var path = Path.Combine(Path.GetTempPath(), $"{po.PoNumber}.html");
                File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                Show(InfoBarSeverity.Informational, "Opened in your browser. In the print window choose \"Save as PDF\".");
            }
            catch (Exception ex)
            {
                Show(InfoBarSeverity.Error, "Could not open the print page: " + ex.Message);
            }
        }

        // ================= STATUS / DELETE =================
        private async void Receive_Click(object sender, RoutedEventArgs e) => await ChangeStatusAsync(sender, "Received");
        private async void CancelOrder_Click(object sender, RoutedEventArgs e) => await ChangeStatusAsync(sender, "Cancelled");

        private async Task ChangeStatusAsync(object sender, string status)
        {
            if (sender is not FrameworkElement { Tag: int id }) return;
            var po = _orders.FirstOrDefault(o => o.PurchaseOrderId == id);
            if (po == null) return;

            var ok = status == "Received"
                ? await ConfirmAsync("Mark as received?", $"Confirm that all items of {po.PoNumber} have arrived. {po.TotalText} will be recorded as an expense. This cannot be undone.", "Mark Received")
                : await ConfirmAsync("Cancel this order?", $"Cancel {po.PoNumber}? This cannot be undone.", "Cancel Order");
            if (!ok) return;

            try
            {
                await ApiClient.PutAsync($"purchaseorders/{id}/status", new { status });
                Show(InfoBarSeverity.Success, $"{po.PoNumber} marked as {status}.");
                await LoadOrdersAsync();
            }
            catch (Exception ex)
            {
                Show(InfoBarSeverity.Error, ex.Message);
            }
        }

        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement { Tag: int id }) return;
            var po = _orders.FirstOrDefault(o => o.PurchaseOrderId == id);
            if (po == null) return;

            if (!await ConfirmAsync("Delete purchase order?", $"Delete {po.PoNumber} permanently?", "Delete")) return;

            try
            {
                await ApiClient.DeleteAsync($"purchaseorders/{id}");
                Show(InfoBarSeverity.Success, $"{po.PoNumber} deleted.");
                await LoadOrdersAsync();
            }
            catch (Exception ex)
            {
                Show(InfoBarSeverity.Error, ex.Message);
            }
        }

        // ================= HELPERS =================
        private static Grid NewLineGrid()
        {
            var g = new Grid { ColumnSpacing = 8 };
            foreach (var w in new[] { new GridLength(1, GridUnitType.Star), new GridLength(90), new GridLength(110), new GridLength(110), new GridLength(44) })
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = w });
            return g;
        }

        private static void AddCell(Grid g, int col, string text, bool bold = false)
        {
            var t = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            if (bold) t.FontWeight = FontWeights.SemiBold;
            Grid.SetColumn(t, col);
            g.Children.Add(t);
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