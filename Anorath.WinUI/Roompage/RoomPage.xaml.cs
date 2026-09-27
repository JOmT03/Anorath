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
    // One row in the rooms table (Model)
    public class RoomItem
    {
        public int RoomId { get; set; }
        public string RoomNumber { get; set; } = "";
        public string RoomType { get; set; } = "";
        public decimal Rate { get; set; }
        public string Status { get; set; } = "";
        public bool IsActive { get; set; }

        public string RateText => "₱" + Rate.ToString("N2");
        public string ActiveText => IsActive ? "Yes" : "No";
    }

    public sealed partial class RoomsPage : Page
    {
        private static readonly string[] RoomTypes = { "Standard", "Deluxe", "Suite", "Family", "Cottage" };

        private readonly ObservableCollection<RoomItem> _rooms = new();
        private List<string> _statuses = new();
        private bool _ready;

        public RoomsPage()
        {
            InitializeComponent();
            RoomsList.ItemsSource = _rooms;
            Loaded += async (s, e) => await InitAsync();
        }

        private async Task InitAsync()
        {
            // Room statuses (same list the API validates against)
            _statuses = new List<string> { "Available", "Occupied", "Cleaning", "Maintenance" };
            StatusFilter.ItemsSource = new[] { "All" }.Concat(_statuses).ToList();
            StatusFilter.SelectedIndex = 0;

            _ready = true;
            await LoadAsync();
        }

        // READ + SEARCH + FILTER
        private async Task LoadAsync()
        {
            try
            {
                var query = new List<string>();
                var search = SearchBox.Text.Trim();
                if (!string.IsNullOrEmpty(search)) query.Add("search=" + Uri.EscapeDataString(search));
                if (StatusFilter.SelectedItem is string status && status != "All") query.Add("status=" + Uri.EscapeDataString(status));

                var url = "rooms" + (query.Count > 0 ? "?" + string.Join("&", query) : "");
                var list = await ApiClient.GetAsync<List<RoomItem>>(url);

                _rooms.Clear();
                foreach (var r in list) _rooms.Add(r);
                EmptyText.Visibility = _rooms.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
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
            if (await ShowRoomDialogAsync(null))
            {
                Show(InfoBarSeverity.Success, "Room added.");
                await LoadAsync();
            }
        }

        // UPDATE
        private async void Edit_Click(object sender, RoutedEventArgs e)
        {
            var room = FindRoom(sender);
            if (room == null) return;

            if (await ShowRoomDialogAsync(room))
            {
                Show(InfoBarSeverity.Success, "Room updated.");
                await LoadAsync();
            }
        }

        // DELETE
        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            var room = FindRoom(sender);
            if (room == null) return;

            var confirm = new ContentDialog
            {
                Title = "Delete room?",
                Content = $"Delete room {room.RoomNumber} ({room.RoomType})? This cannot be undone.",
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot
            };
            if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;

            try
            {
                await ApiClient.DeleteAsync($"rooms/{room.RoomId}");
                Show(InfoBarSeverity.Success, "Room deleted.");
                await LoadAsync();
            }
            catch (Exception ex)
            {
                Show(InfoBarSeverity.Error, ex.Message);   // e.g. "This room has reservations..."
            }
        }

        private RoomItem? FindRoom(object sender) =>
            sender is Button { Tag: int id } ? _rooms.FirstOrDefault(r => r.RoomId == id) : null;

        // Add / Edit form (existing == null means Add)
        private async Task<bool> ShowRoomDialogAsync(RoomItem? existing)
        {
            var numberBox = new TextBox { Header = "Room number", Text = existing?.RoomNumber ?? "", MaxLength = 20 };
            var typeBox = new ComboBox
            {
                Header = "Room type",
                ItemsSource = RoomTypes,
                IsEditable = true,   // pick from the list or type a new type
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Text = existing?.RoomType ?? RoomTypes[0]
            };
            var rateBox = new NumberBox
            {
                Header = "Rate per night (₱)",
                Minimum = 0,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
                Value = existing != null ? (double)existing.Rate : 1500
            };
            var statusBox = new ComboBox
            {
                Header = "Status",
                ItemsSource = _statuses,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                SelectedItem = existing?.Status ?? "Available"
            };
            var activeBox = new CheckBox { Content = "Active (can be booked)", IsChecked = existing?.IsActive ?? true };
            var errorText = new TextBlock { Foreground = new SolidColorBrush(Colors.Firebrick), TextWrapping = TextWrapping.Wrap };

            var form = new StackPanel { Spacing = 12, Width = 360 };
            form.Children.Add(numberBox);
            form.Children.Add(typeBox);
            form.Children.Add(rateBox);
            form.Children.Add(statusBox);
            form.Children.Add(activeBox);
            form.Children.Add(errorText);

            var dialog = new ContentDialog
            {
                Title = existing == null ? "Add Room" : $"Edit Room {existing.RoomNumber}",
                Content = form,
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
                    var roomType = (typeBox.SelectedItem as string) ?? typeBox.Text;

                    // Client-side validation (the API checks again)
                    if (string.IsNullOrWhiteSpace(numberBox.Text) || string.IsNullOrWhiteSpace(roomType))
                    {
                        errorText.Text = "Room number and type are required.";
                        args.Cancel = true;
                        return;
                    }
                    if (double.IsNaN(rateBox.Value) || rateBox.Value <= 0)
                    {
                        errorText.Text = "Rate must be greater than zero.";
                        args.Cancel = true;
                        return;
                    }
                    if (statusBox.SelectedItem == null)
                    {
                        errorText.Text = "Please choose a status.";
                        args.Cancel = true;
                        return;
                    }

                    var body = new
                    {
                        roomNumber = numberBox.Text.Trim(),
                        roomType = roomType.Trim(),
                        rate = (decimal)rateBox.Value,
                        status = statusBox.SelectedItem.ToString(),
                        isActive = activeBox.IsChecked == true
                    };

                    if (existing == null)
                        await ApiClient.PostAsync("rooms", body);
                    else
                        await ApiClient.PutAsync($"rooms/{existing.RoomId}", body);
                }
                catch (Exception ex)
                {
                    errorText.Text = ex.Message;   // e.g. "Room 101 already exists."
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