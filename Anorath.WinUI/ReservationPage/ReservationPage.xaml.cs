using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Anorath.WinUI.Services;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Anorath.WinUI.Pages
{
    // One row in the reservations table (Model)
    public class ReservationItem
    {
        public int ReservationId { get; set; }
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = "";
        public int RoomId { get; set; }
        public string RoomNumber { get; set; } = "";
        public string RoomType { get; set; } = "";
        public DateTime CheckIn { get; set; }
        public DateTime CheckOut { get; set; }
        public int NumberOfGuests { get; set; }
        public int Nights { get; set; }
        public decimal Amount { get; set; }
        public string Status { get; set; } = "";
        public string? Remarks { get; set; }

        public string StayText => $"{CheckIn:MMM d} – {CheckOut:MMM d, yyyy} ({Nights}n)";
        public string AmountText => "₱" + Amount.ToString("N2");
        public bool CanEdit => Status == "Pending" || Status == "Confirmed";
        public bool CanChangeStatus => Status != "CheckedOut" && Status != "Cancelled";
        public bool CanDelete => Status == "Pending" || Status == "Cancelled";
    }

    public sealed partial class ReservationsPage : Page
    {
        private static readonly string[] Statuses = { "Pending", "Confirmed", "CheckedIn", "CheckedOut", "Cancelled" };

        // Same life cycle the API enforces
        private static readonly Dictionary<string, string[]> NextStatuses = new()
        {
            ["Pending"] = new[] { "Confirmed", "Cancelled" },
            ["Confirmed"] = new[] { "CheckedIn", "Cancelled" },
            ["CheckedIn"] = new[] { "CheckedOut" },
        };

        private readonly ObservableCollection<ReservationItem> _reservations = new();
        private bool _ready;

        public ReservationsPage()
        {
            InitializeComponent();
            ReservationsList.ItemsSource = _reservations;
            StatusFilter.ItemsSource = new[] { "All" }.Concat(Statuses).ToList();
            StatusFilter.SelectedIndex = 0;
            Loaded += async (s, e) => { _ready = true; await LoadAsync(); };
        }

        // READ + SEARCH + FILTER
        private async Task LoadAsync()
        {
            try
            {
                var query = new List<string>();
                var search = SearchBox.Text.Trim();
                if (!string.IsNullOrEmpty(search)) query.Add("search=" + Uri.EscapeDataString(search));
                if (StatusFilter.SelectedItem is string status && status != "All") query.Add("status=" + status);

                var url = "reservations" + (query.Count > 0 ? "?" + string.Join("&", query) : "");
                var list = await ApiClient.GetAsync<List<ReservationItem>>(url);

                _reservations.Clear();
                foreach (var r in list) _reservations.Add(r);
                EmptyText.Visibility = _reservations.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                Show(InfoBarSeverity.Error, ex.Message);
            }
        }

        private async void Search_Click(object sender, RoutedEventArgs e) => await LoadAsync();

        private async void ShowAll_Click(object sender, RoutedEventArgs e)
        {
            SearchBox.Text = "";
            StatusFilter.SelectedIndex = 0;
            await LoadAsync();
        }

        private async void SearchBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter) await LoadAsync();
        }

        private async void StatusFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_ready) await LoadAsync();
        }

        // CREATE
        private async void Add_Click(object sender, RoutedEventArgs e)
        {
            if (await ShowReservationDialogAsync(null))
            {
                Show(InfoBarSeverity.Success, "Reservation created (Pending).");
                await LoadAsync();
            }
        }

        // UPDATE
        private async void Edit_Click(object sender, RoutedEventArgs e)
        {
            var reservation = FindReservation(sender);
            if (reservation == null) return;

            if (await ShowReservationDialogAsync(reservation))
            {
                Show(InfoBarSeverity.Success, "Reservation updated.");
                await LoadAsync();
            }
        }

        // STATUS: Confirm, Check in, Check out, Cancel
        private async void Status_Click(object sender, RoutedEventArgs e)
        {
            var reservation = FindReservation(sender);
            if (reservation == null || !NextStatuses.TryGetValue(reservation.Status, out var options)) return;

            var statusBox = new ComboBox { Header = "New status", ItemsSource = options, SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
            var info = new TextBlock
            {
                Text = $"{reservation.CustomerName} · Room {reservation.RoomNumber}\nCurrent status: {reservation.Status}",
                Opacity = 0.8
            };
            var form = new StackPanel { Spacing = 12, Width = 320 };
            form.Children.Add(info);
            form.Children.Add(statusBox);

            var dialog = new ContentDialog
            {
                Title = "Update status",
                Content = form,
                PrimaryButtonText = "Update",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

            try
            {
                var newStatus = statusBox.SelectedItem?.ToString() ?? "";
                await ApiClient.PutAsync($"reservations/{reservation.ReservationId}/status", new { status = newStatus });
                Show(InfoBarSeverity.Success, $"Reservation is now {newStatus}.");
                await LoadAsync();
            }
            catch (Exception ex)
            {
                Show(InfoBarSeverity.Error, ex.Message);
            }
        }

        // DELETE
        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            var reservation = FindReservation(sender);
            if (reservation == null) return;

            var confirm = new ContentDialog
            {
                Title = "Delete reservation?",
                Content = $"Delete the {reservation.Status} reservation of {reservation.CustomerName} (Room {reservation.RoomNumber})?",
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot
            };
            if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;

            try
            {
                await ApiClient.DeleteAsync($"reservations/{reservation.ReservationId}");
                Show(InfoBarSeverity.Success, "Reservation deleted.");
                await LoadAsync();
            }
            catch (Exception ex)
            {
                Show(InfoBarSeverity.Error, ex.Message);
            }
        }

        private ReservationItem? FindReservation(object sender) =>
            sender is Button { Tag: int id } ? _reservations.FirstOrDefault(r => r.ReservationId == id) : null;

        // New / Edit form (existing == null means New)
        private async Task<bool> ShowReservationDialogAsync(ReservationItem? existing)
        {
            List<CustomerItem> customers;
            List<RoomItem> rooms;
            try
            {
                customers = (await ApiClient.GetAsync<List<CustomerItem>>("customers")).Where(c => c.IsActive).ToList();
                rooms = (await ApiClient.GetAsync<List<RoomItem>>("rooms"))
                    .Where(r => r.IsActive && r.Status != "Maintenance").ToList();
            }
            catch (Exception ex)
            {
                Show(InfoBarSeverity.Error, ex.Message);
                return false;
            }

            if (customers.Count == 0 || rooms.Count == 0)
            {
                Show(InfoBarSeverity.Warning, "Add at least one active customer and one active room first.");
                return false;
            }

            // ComboBoxes show text; the matching object is found by index
            var customerBox = new ComboBox
            {
                Header = "Guest",
                ItemsSource = customers.Select(c => $"{c.CustomerName} ({c.CustomerCode})").ToList(),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                SelectedIndex = existing == null ? 0 : Math.Max(0, customers.FindIndex(c => c.CustomerId == existing.CustomerId))
            };
            var roomBox = new ComboBox
            {
                Header = "Room",
                ItemsSource = rooms.Select(r => $"{r.RoomNumber} · {r.RoomType} · {r.RateText}/night").ToList(),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                SelectedIndex = existing == null ? 0 : Math.Max(0, rooms.FindIndex(r => r.RoomId == existing.RoomId))
            };
            var checkInBox = new CalendarDatePicker
            {
                Header = "Check-in",
                Date = existing?.CheckIn ?? DateTime.Today,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            var checkOutBox = new CalendarDatePicker
            {
                Header = "Check-out",
                Date = existing?.CheckOut ?? DateTime.Today.AddDays(1),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            if (existing == null) checkInBox.MinDate = DateTime.Today;

            var guestsBox = new NumberBox
            {
                Header = "Number of guests",
                Minimum = 1,
                Maximum = 20,
                Value = existing?.NumberOfGuests ?? 1,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact
            };
            var remarksBox = new TextBox { Header = "Remarks (optional)", Text = existing?.Remarks ?? "", MaxLength = 250 };
            var totalText = new TextBlock { FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
            var errorText = new TextBlock { Foreground = new SolidColorBrush(Colors.Firebrick), TextWrapping = TextWrapping.Wrap };

            // Live total: nights x room rate
            void UpdateTotal()
            {
                if (checkInBox.Date is DateTimeOffset a && checkOutBox.Date is DateTimeOffset b && roomBox.SelectedIndex >= 0)
                {
                    var nights = (b.Date - a.Date).Days;
                    totalText.Text = nights > 0
                        ? $"{nights} night(s) × {rooms[roomBox.SelectedIndex].RateText} = ₱{nights * rooms[roomBox.SelectedIndex].Rate:N2}"
                        : "Check-out must be after check-in.";
                }
            }
            checkInBox.DateChanged += (s, e) => UpdateTotal();
            checkOutBox.DateChanged += (s, e) => UpdateTotal();
            roomBox.SelectionChanged += (s, e) => UpdateTotal();
            UpdateTotal();

            var form = new StackPanel { Spacing = 10, Width = 380 };
            form.Children.Add(customerBox);
            form.Children.Add(roomBox);
            form.Children.Add(checkInBox);
            form.Children.Add(checkOutBox);
            form.Children.Add(guestsBox);
            form.Children.Add(remarksBox);
            form.Children.Add(totalText);
            form.Children.Add(errorText);

            var dialog = new ContentDialog
            {
                Title = existing == null ? "New Reservation" : $"Edit Reservation #{existing.ReservationId}",
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
                    // Client-side validation (the API checks again, including double booking)
                    if (customerBox.SelectedIndex < 0 || roomBox.SelectedIndex < 0 ||
                        checkInBox.Date is not DateTimeOffset checkIn || checkOutBox.Date is not DateTimeOffset checkOut)
                    {
                        errorText.Text = "Guest, room and both dates are required.";
                        args.Cancel = true;
                        return;
                    }
                    if (checkOut.Date <= checkIn.Date)
                    {
                        errorText.Text = "Check-out must be after check-in.";
                        args.Cancel = true;
                        return;
                    }

                    var body = new
                    {
                        customerId = customers[customerBox.SelectedIndex].CustomerId,
                        roomId = rooms[roomBox.SelectedIndex].RoomId,
                        checkIn = checkIn.Date.ToString("yyyy-MM-dd"),
                        checkOut = checkOut.Date.ToString("yyyy-MM-dd"),
                        numberOfGuests = double.IsNaN(guestsBox.Value) ? 1 : (int)guestsBox.Value,
                        remarks = remarksBox.Text.Trim()
                    };

                    if (existing == null)
                        await ApiClient.PostAsync("reservations", body);
                    else
                        await ApiClient.PutAsync($"reservations/{existing.ReservationId}", body);
                }
                catch (Exception ex)
                {
                    errorText.Text = ex.Message;   // e.g. "Room 101 is already booked for those dates."
                    args.Cancel = true;
                }
                finally
                {
                    deferral.Complete();
                }
            };

            return await dialog.ShowAsync() == ContentDialogResult.Primary;
        }
        // PRINT: Reservation Form & Guest Agreement (hard-copy proof, signed by guest and staff)
        private async void Print_Click(object sender, RoutedEventArgs e)
        {
            var r = FindReservation(sender);
            if (r == null) return;

            // Guest contact details come from the customer record
            string code = "", contact = "", email = "", address = "";
            try
            {
                var customers = await ApiClient.GetAsync<List<JsonElement>>("customers");
                var c = customers.FirstOrDefault(x => Prop(x, "customerId") == r.CustomerId.ToString());
                if (c.ValueKind == JsonValueKind.Object)
                {
                    code = Prop(c, "customerCode");
                    contact = Prop(c, "contactNumber", "contactNo", "phone");
                    email = Prop(c, "email", "emailAddress");
                    address = Prop(c, "address");
                }
            }
            catch
            {
                // Still print the form; contact lines are left blank to be filled by hand
            }

            static string H(string? s) => WebUtility.HtmlEncode(s ?? "");
            string Blank(string v) => string.IsNullOrWhiteSpace(v) ? "<span class='line'></span>" : H(v);

            var resNo = $"RES-{r.ReservationId:00000}";
            var rate = r.Nights > 0 ? r.Amount / r.Nights : r.Amount;

            var sb = new StringBuilder();
            sb.Append("<!doctype html><html><head><meta charset='utf-8'><title>").Append(resNo).Append("</title><style>");
            sb.Append(@"
body{font-family:'Segoe UI',Arial,sans-serif;color:#222;margin:36px auto;max-width:760px;font-size:13px;}
.top{display:flex;justify-content:space-between;align-items:flex-end;border-bottom:3px solid #1f4e79;padding-bottom:10px;}
.company{font-size:22px;font-weight:700;color:#1f4e79;}
.title{font-size:16px;font-weight:700;text-align:right;}
.meta{text-align:right;line-height:1.6;}
h3{font-size:13px;letter-spacing:.5px;color:#1f4e79;border-bottom:1px solid #ccd;padding-bottom:4px;margin:22px 0 8px;}
table.kv{width:100%;border-collapse:collapse;} table.kv td{padding:5px 4px;vertical-align:top;}
table.kv td.k{width:150px;color:#555;}
.total{font-size:16px;font-weight:700;}
.line{display:inline-block;min-width:260px;border-bottom:1px solid #999;height:14px;}
ol{padding-left:18px;line-height:1.55;margin:0;}
.agree{margin-top:14px;font-style:italic;}
.sign{display:flex;justify-content:space-between;margin-top:56px;}
.sign div{width:44%;text-align:center;}
.sign .bar{border-top:1px solid #333;padding-top:6px;}
.small{color:#666;font-size:11px;}
.stamp{position:fixed;top:40%;left:18%;font-size:80px;color:rgba(200,0,0,.15);transform:rotate(-25deg);font-weight:700;}
@media print{body{margin:12mm auto;}}
");
            sb.Append("</style></head><body>");
            if (r.Status == "Cancelled") sb.Append("<div class='stamp'>CANCELLED</div>");

            sb.Append("<div class='top'><div><div class='company'>").Append(H(Session.CompanyName)).Append("</div>");
            sb.Append("<div class='small'>Reservation Form &amp; Guest Agreement</div></div>");
            sb.Append("<div><div class='title'>RESERVATION FORM</div><div class='meta'>");
            sb.Append("Reservation No: <b>").Append(resNo).Append("</b><br>");
            sb.Append("Date issued: ").Append(DateTime.Now.ToString("MMMM dd, yyyy")).Append("<br>");
            sb.Append("Status: <b>").Append(H(r.Status)).Append("</b></div></div></div>");

            sb.Append("<h3>GUEST INFORMATION</h3><table class='kv'>");
            sb.Append("<tr><td class='k'>Guest name</td><td><b>").Append(H(r.CustomerName)).Append("</b>")
              .Append(string.IsNullOrEmpty(code) ? "" : $" ({H(code)})").Append("</td></tr>");
            sb.Append("<tr><td class='k'>Contact number</td><td>").Append(Blank(contact)).Append("</td></tr>");
            sb.Append("<tr><td class='k'>Email</td><td>").Append(Blank(email)).Append("</td></tr>");
            sb.Append("<tr><td class='k'>Address</td><td>").Append(Blank(address)).Append("</td></tr>");
            sb.Append("<tr><td class='k'>Valid ID presented</td><td><span class='line'></span></td></tr>");
            sb.Append("</table>");

            sb.Append("<h3>STAY DETAILS</h3><table class='kv'>");
            sb.Append("<tr><td class='k'>Room</td><td>").Append(H(r.RoomNumber)).Append(" · ").Append(H(r.RoomType)).Append("</td></tr>");
            sb.Append("<tr><td class='k'>Check-in</td><td>").Append(r.CheckIn.ToString("dddd, MMMM dd, yyyy")).Append(" · from 2:00 PM</td></tr>");
            sb.Append("<tr><td class='k'>Check-out</td><td>").Append(r.CheckOut.ToString("dddd, MMMM dd, yyyy")).Append(" · until 12:00 NN</td></tr>");
            sb.Append("<tr><td class='k'>Number of guests</td><td>").Append(r.NumberOfGuests).Append("</td></tr>");
            sb.Append("<tr><td class='k'>Rate</td><td>₱").Append(rate.ToString("N2")).Append(" per night × ").Append(r.Nights).Append(" night(s)</td></tr>");
            sb.Append("<tr><td class='k'>Total amount</td><td class='total'>₱").Append(r.Amount.ToString("N2")).Append("</td></tr>");
            if (!string.IsNullOrWhiteSpace(r.Remarks))
                sb.Append("<tr><td class='k'>Remarks</td><td>").Append(H(r.Remarks)).Append("</td></tr>");
            sb.Append("</table>");

            sb.Append("<h3>TERMS &amp; CONDITIONS</h3><ol>");
            sb.Append("<li>Check-in time is 2:00 PM and check-out time is 12:00 noon. Late check-out is subject to availability and additional charges.</li>");
            sb.Append("<li>A valid government-issued ID must be presented upon check-in.</li>");
            sb.Append("<li>Cancellations made at least 48 hours before the check-in date are free of charge. Later cancellations or no-shows may be charged one (1) night.</li>");
            sb.Append("<li>The number of guests may not exceed the room capacity stated above without prior approval.</li>");
            sb.Append("<li>The guest is liable for any loss of or damage to resort property caused by the guest or companions.</li>");
            sb.Append("<li>Restaurant and other charges made to the room must be settled upon check-out.</li>");
            sb.Append("<li>The resort is not responsible for valuables not deposited at the front desk.</li>");
            sb.Append("</ol>");
            sb.Append("<p class='agree'>I have read and agree to the terms and conditions above, and confirm that the information in this form is correct.</p>");

            sb.Append("<div class='sign'>");
            sb.Append("<div><div style='height:18px'>").Append(H(r.CustomerName)).Append("</div><div class='bar'>Guest signature over printed name</div><div class='small'>Date: ____________</div></div>");
            sb.Append("<div><div style='height:18px'>").Append(H(Session.FullName)).Append("</div><div class='bar'>Received by (").Append(H(Session.Role)).Append(")</div><div class='small'>Date: ____________</div></div>");
            sb.Append("</div>");

            sb.Append("<p class='small' style='margin-top:30px'>Generated ").Append(DateTime.Now.ToString("MMM dd, yyyy h:mm tt"))
              .Append(" · Keep this form as proof of reservation.</p>");
            sb.Append("<script>window.onload=function(){window.print();}</script></body></html>");

            try
            {
                var path = Path.Combine(Path.GetTempPath(), $"{resNo}.html");
                File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                Show(InfoBarSeverity.Informational, "Reservation form opened in your browser. Print it, or choose \"Save as PDF\".");
            }
            catch (Exception ex)
            {
                Show(InfoBarSeverity.Error, "Could not open the print page: " + ex.Message);
            }
        }

        // Reads the first property that exists (API field names are camelCase)
        private static string Prop(JsonElement e, params string[] names)
        {
            foreach (var n in names)
                if (e.TryGetProperty(n, out var v) && v.ValueKind != JsonValueKind.Null)
                    return v.ToString();
            return "";
        }
        private void Show(InfoBarSeverity severity, string message)
        {
            MessageBar.Severity = severity;
            MessageBar.Message = message;
            MessageBar.IsOpen = true;
        }
    }
}