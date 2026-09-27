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
    // One supplier (Model)
    public class SupplierRow
    {
        public int SupplierId { get; set; }
        public string SupplierCode { get; set; } = "";
        public string SupplierName { get; set; } = "";
        public string? ContactPerson { get; set; }
        public string? ContactNumber { get; set; }
        public string? EmailAddress { get; set; }
        public string? Address { get; set; }
        public bool IsActive { get; set; }

        public string SubText =>
            $"{SupplierCode} · {(string.IsNullOrWhiteSpace(ContactPerson) ? "No contact person" : ContactPerson)}"
            + (IsActive ? "" : " · Inactive");
    }

    // One item in a supplier's price list (Model). Also used by the PO page.
    public class SupplierItemRow
    {
        public int SupplierItemId { get; set; }
        public int SupplierId { get; set; }
        public string ItemName { get; set; } = "";
        public string Unit { get; set; } = "";
        public decimal UnitPrice { get; set; }
        public bool IsActive { get; set; }

        public string PriceText => "₱" + UnitPrice.ToString("N2");
        public string ActiveText => IsActive ? "Yes" : "No";
    }

    public sealed partial class SuppliersPage : Page
    {
        private static readonly string[] Units = { "pcs", "box", "pack", "set", "kg", "liter", "meter", "roll", "gallon", "sack" };

        private readonly ObservableCollection<SupplierRow> _suppliers = new();
        private readonly ObservableCollection<SupplierItemRow> _items = new();

        public SuppliersPage()
        {
            InitializeComponent();
            SuppliersList.ItemsSource = _suppliers;
            ItemsList.ItemsSource = _items;
            Loaded += async (s, e) => await LoadSuppliersAsync();
        }

        private SupplierRow? SelectedSupplier => SuppliersList.SelectedItem as SupplierRow;

        // ---------- Suppliers ----------

        private async Task LoadSuppliersAsync(int? selectId = null)
        {
            try
            {
                var keepId = selectId ?? SelectedSupplier?.SupplierId;
                var search = SearchBox.Text.Trim();
                var url = string.IsNullOrEmpty(search) ? "suppliers" : $"suppliers?search={Uri.EscapeDataString(search)}";
                var list = await ApiClient.GetAsync<List<SupplierRow>>(url);

                _suppliers.Clear();
                foreach (var s in list) _suppliers.Add(s);
                EmptySuppliersText.Visibility = _suppliers.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

                // Keep the same supplier selected after a reload
                SuppliersList.SelectedItem = _suppliers.FirstOrDefault(s => s.SupplierId == keepId);
                if (SuppliersList.SelectedItem == null) await LoadItemsAsync();
            }
            catch (Exception ex)
            {
                Show(InfoBarSeverity.Error, ex.Message);
            }
        }

        private async void Search_Click(object sender, RoutedEventArgs e) => await LoadSuppliersAsync();

        private async void SearchBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter) await LoadSuppliersAsync();
        }

        private async void SuppliersList_SelectionChanged(object sender, SelectionChangedEventArgs e) => await LoadItemsAsync();

        private async void AddSupplier_Click(object sender, RoutedEventArgs e)
        {
            var newId = await ShowSupplierDialogAsync(null);
            if (newId != null)
            {
                Show(InfoBarSeverity.Success, "Supplier added. Now add the items you buy from them.");
                await LoadSuppliersAsync(newId);
            }
        }

        private async void EditSupplier_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement { Tag: int id }) return;
            var supplier = _suppliers.FirstOrDefault(s => s.SupplierId == id);
            if (supplier == null) return;

            if (await ShowSupplierDialogAsync(supplier) != null)
            {
                Show(InfoBarSeverity.Success, "Supplier updated.");
                await LoadSuppliersAsync(id);
            }
        }

        private async void DeleteSupplier_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement { Tag: int id }) return;
            var supplier = _suppliers.FirstOrDefault(s => s.SupplierId == id);
            if (supplier == null) return;

            if (!await ConfirmAsync("Delete supplier?", $"Delete {supplier.SupplierName} and its price list?")) return;

            try
            {
                await ApiClient.DeleteAsync($"suppliers/{id}");
                Show(InfoBarSeverity.Success, "Supplier deleted.");
                await LoadSuppliersAsync();
            }
            catch (Exception ex)
            {
                Show(InfoBarSeverity.Error, ex.Message);   // e.g. "This supplier has purchase orders..."
            }
        }

        // Returns the supplier id when saved, or null when cancelled
        private async Task<int?> ShowSupplierDialogAsync(SupplierRow? existing)
        {
            var nameBox = new TextBox { Header = "Supplier name", Text = existing?.SupplierName ?? "", MaxLength = 150, PlaceholderText = "ACDC Electro" };
            var personBox = new TextBox { Header = "Contact person", Text = existing?.ContactPerson ?? "", MaxLength = 150 };
            var numberBox = new TextBox { Header = "Contact number", Text = existing?.ContactNumber ?? "", MaxLength = 20 };
            var emailBox = new TextBox { Header = "Email", Text = existing?.EmailAddress ?? "", MaxLength = 150 };
            var addressBox = new TextBox { Header = "Address", Text = existing?.Address ?? "", MaxLength = 250 };
            var activeBox = new CheckBox { Content = "Active", IsChecked = existing?.IsActive ?? true };
            var errorText = new TextBlock { Foreground = new SolidColorBrush(Colors.Firebrick), TextWrapping = TextWrapping.Wrap };

            var form = new StackPanel { Spacing = 10, Width = 360 };
            form.Children.Add(nameBox);
            form.Children.Add(personBox);
            form.Children.Add(numberBox);
            form.Children.Add(emailBox);
            form.Children.Add(addressBox);
            form.Children.Add(activeBox);
            form.Children.Add(errorText);

            int? savedId = null;
            var dialog = new ContentDialog
            {
                Title = existing == null ? "Add Supplier" : $"Edit {existing.SupplierName}",
                Content = new ScrollViewer { Content = form, MaxHeight = 500 },
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
                    if (nameBox.Text.Trim().Length < 2)
                    {
                        errorText.Text = "Supplier name is required.";
                        args.Cancel = true;
                        return;
                    }

                    var body = new
                    {
                        supplierName = nameBox.Text.Trim(),
                        contactPerson = personBox.Text.Trim(),
                        contactNumber = numberBox.Text.Trim(),
                        emailAddress = emailBox.Text.Trim(),
                        address = addressBox.Text.Trim(),
                        isActive = activeBox.IsChecked == true
                    };

                    if (existing == null)
                    {
                        var created = await ApiClient.PostAsync<SupplierRow>("suppliers", body);
                        savedId = created.SupplierId;
                    }
                    else
                    {
                        await ApiClient.PutAsync($"suppliers/{existing.SupplierId}", body);
                        savedId = existing.SupplierId;
                    }
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

            return await dialog.ShowAsync() == ContentDialogResult.Primary ? savedId : null;
        }

        // ---------- Items of the selected supplier ----------

        private async Task LoadItemsAsync()
        {
            _items.Clear();
            var supplier = SelectedSupplier;

            if (supplier == null)
            {
                ItemsTitle.Text = "Items";
                AddItemButton.IsEnabled = false;
                EmptyItemsText.Text = "Select a supplier on the left to see its items.";
                EmptyItemsText.Visibility = Visibility.Visible;
                return;
            }

            ItemsTitle.Text = $"Items from {supplier.SupplierName}";
            AddItemButton.IsEnabled = true;

            try
            {
                var list = await ApiClient.GetAsync<List<SupplierItemRow>>($"suppliers/{supplier.SupplierId}/items");
                foreach (var i in list) _items.Add(i);

                EmptyItemsText.Text = "No items yet. Click + Add Item.";
                EmptyItemsText.Visibility = _items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                Show(InfoBarSeverity.Error, ex.Message);
            }
        }

        private async void AddItem_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedSupplier == null) return;
            if (await ShowItemDialogAsync(null))
            {
                Show(InfoBarSeverity.Success, "Item added.");
                await LoadItemsAsync();
            }
        }

        private async void EditItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement { Tag: int id }) return;
            var item = _items.FirstOrDefault(i => i.SupplierItemId == id);
            if (item == null) return;

            if (await ShowItemDialogAsync(item))
            {
                Show(InfoBarSeverity.Success, "Item updated.");
                await LoadItemsAsync();
            }
        }

        private async void DeleteItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement { Tag: int id }) return;
            var item = _items.FirstOrDefault(i => i.SupplierItemId == id);
            if (item == null) return;

            if (!await ConfirmAsync("Delete item?", $"Delete {item.ItemName} from this supplier's list?")) return;

            try
            {
                await ApiClient.DeleteAsync($"suppliers/items/{id}");
                Show(InfoBarSeverity.Success, "Item deleted.");
                await LoadItemsAsync();
            }
            catch (Exception ex)
            {
                Show(InfoBarSeverity.Error, ex.Message);   // e.g. "This item was already ordered..."
            }
        }

        private async Task<bool> ShowItemDialogAsync(SupplierItemRow? existing)
        {
            var supplier = SelectedSupplier!;

            var nameBox = new TextBox { Header = "Item name", Text = existing?.ItemName ?? "", MaxLength = 150, PlaceholderText = "LED Bulb 12W" };
            var unitBox = new ComboBox
            {
                Header = "Unit",
                ItemsSource = Units,
                IsEditable = true,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Text = existing?.Unit ?? "pcs"
            };
            var priceBox = new NumberBox
            {
                Header = "Unit price (₱)",
                Minimum = 0,
                Value = existing != null ? (double)existing.UnitPrice : 0,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact
            };
            var activeBox = new CheckBox { Content = "Active (can be ordered)", IsChecked = existing?.IsActive ?? true };
            var errorText = new TextBlock { Foreground = new SolidColorBrush(Colors.Firebrick), TextWrapping = TextWrapping.Wrap };

            var form = new StackPanel { Spacing = 10, Width = 340 };
            form.Children.Add(new TextBlock { Text = "Supplier: " + supplier.SupplierName, Opacity = 0.7 });
            form.Children.Add(nameBox);
            form.Children.Add(unitBox);
            form.Children.Add(priceBox);
            form.Children.Add(activeBox);
            form.Children.Add(errorText);

            var dialog = new ContentDialog
            {
                Title = existing == null ? "Add Item" : $"Edit {existing.ItemName}",
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
                    var unit = ((unitBox.SelectedItem as string) ?? unitBox.Text ?? "").Trim();

                    if (nameBox.Text.Trim().Length < 2 || unit.Length == 0)
                    {
                        errorText.Text = "Item name and unit are required.";
                        args.Cancel = true;
                        return;
                    }
                    if (double.IsNaN(priceBox.Value) || priceBox.Value <= 0)
                    {
                        errorText.Text = "Unit price must be greater than zero.";
                        args.Cancel = true;
                        return;
                    }

                    var body = new
                    {
                        itemName = nameBox.Text.Trim(),
                        unit,
                        unitPrice = (decimal)priceBox.Value,
                        isActive = activeBox.IsChecked == true
                    };

                    if (existing == null)
                        await ApiClient.PostAsync($"suppliers/{supplier.SupplierId}/items", body);
                    else
                        await ApiClient.PutAsync($"suppliers/items/{existing.SupplierItemId}", body);
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

        // ---------- Helpers ----------

        // Show the "⋯" button clearly only while the mouse is over the row
        private void Row_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Grid g && g.Children.OfType<Button>().LastOrDefault() is Button b) b.Opacity = 1;
        }

        private void Row_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Grid g && g.Children.OfType<Button>().LastOrDefault() is Button b) b.Opacity = 0.3;
        }

        private async Task<bool> ConfirmAsync(string title, string message)
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = message,
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel",
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