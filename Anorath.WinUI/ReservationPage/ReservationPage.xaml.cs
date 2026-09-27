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

        private void Show(InfoBarSeverity severity, string message)
        {
            MessageBar.Severity = severity;
            MessageBar.Message = message;
            MessageBar.IsOpen = true;
        }
    }
}