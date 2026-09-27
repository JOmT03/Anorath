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
    // One menu item (Model). Also used by the POS page.
    public class ProductItem
    {
        public int ProductId { get; set; }
        public string ProductCode { get; set; } = "";
        public string ProductName { get; set; } = "";
        public decimal Price { get; set; }
        public int StockQuantity { get; set; }
        public bool IsActive { get; set; }

        public string PriceText => "₱" + Price.ToString("N2");
        public string ActiveText => IsActive ? "Yes" : "No";

        // Only Admin/Manager may change the menu (the API enforces this too)
        public bool CanManage => Session.Role == "Admin" || Session.Role == "Manager";
    }

    public sealed partial class MenuPage : Page
    {
        private readonly ObservableCollection<ProductItem> _products = new();

        public MenuPage()
        {
            InitializeComponent();
            ProductsList.ItemsSource = _products;

            // Cashiers can view the menu but not change it
            if (Session.Role != "Admin" && Session.Role != "Manager")
                AddButton.Visibility = Visibility.Collapsed;

            Loaded += async (s, e) => await LoadAsync();
        }

        // READ + SEARCH
        private async Task LoadAsync()
        {
            try
            {
                var search = SearchBox.Text.Trim();
                var url = string.IsNullOrEmpty(search) ? "products" : $"products?search={Uri.EscapeDataString(search)}";
                var list = await ApiClient.GetAsync<List<ProductItem>>(url);

                _products.Clear();
                foreach (var p in list) _products.Add(p);
                EmptyText.Visibility = _products.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
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
            await LoadAsync();
        }

        private async void SearchBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter) await LoadAsync();
        }

        private async void Add_Click(object sender, RoutedEventArgs e)
        {
            if (await ShowProductDialogAsync(null))
            {
                Show(InfoBarSeverity.Success, "Menu item added.");
                await LoadAsync();
            }
        }

        private async void Edit_Click(object sender, RoutedEventArgs e)
        {
            var product = FindProduct(sender);
            if (product == null) return;

            if (await ShowProductDialogAsync(product))
            {
                Show(InfoBarSeverity.Success, "Menu item updated.");
                await LoadAsync();
            }
        }

        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            var product = FindProduct(sender);
            if (product == null) return;

            var confirm = new ContentDialog
            {
                Title = "Delete menu item?",
                Content = $"Delete {product.ProductName} ({product.ProductCode})? This cannot be undone.",
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot
            };
            if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;

            try
            {
                await ApiClient.DeleteAsync($"products/{product.ProductId}");
                Show(InfoBarSeverity.Success, "Menu item deleted.");
                await LoadAsync();
            }
            catch (Exception ex)
            {
                Show(InfoBarSeverity.Error, ex.Message);
            }
        }

        private ProductItem? FindProduct(object sender) =>
            sender is Button { Tag: int id } ? _products.FirstOrDefault(p => p.ProductId == id) : null;

        private async Task<bool> ShowProductDialogAsync(ProductItem? existing)
        {
            var nameBox = new TextBox { Header = "Item name", Text = existing?.ProductName ?? "", MaxLength = 150 };
            var priceBox = new NumberBox
            {
                Header = "Price (₱)",
                Minimum = 0,
                Value = existing != null ? (double)existing.Price : 150,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact
            };
            var stockBox = new NumberBox
            {
                Header = "Stock on hand",
                Minimum = 0,
                Value = existing?.StockQuantity ?? 50,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact
            };
            var activeBox = new CheckBox { Content = "Active (available to order)", IsChecked = existing?.IsActive ?? true };
            var errorText = new TextBlock { Foreground = new SolidColorBrush(Colors.Firebrick), TextWrapping = TextWrapping.Wrap };

            var form = new StackPanel { Spacing = 12, Width = 340 };
            if (existing != null)
                form.Children.Add(new TextBlock { Text = "Code: " + existing.ProductCode, Opacity = 0.7 });
            form.Children.Add(nameBox);
            form.Children.Add(priceBox);
            form.Children.Add(stockBox);
            form.Children.Add(activeBox);
            form.Children.Add(errorText);

            var dialog = new ContentDialog
            {
                Title = existing == null ? "Add Menu Item" : $"Edit {existing.ProductName}",
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
                    if (nameBox.Text.Trim().Length < 2)
                    {
                        errorText.Text = "Item name is required.";
                        args.Cancel = true;
                        return;
                    }
                    if (double.IsNaN(priceBox.Value) || priceBox.Value <= 0)
                    {
                        errorText.Text = "Price must be greater than zero.";
                        args.Cancel = true;
                        return;
                    }

                    var body = new
                    {
                        productName = nameBox.Text.Trim(),
                        price = (decimal)priceBox.Value,
                        stockQuantity = double.IsNaN(stockBox.Value) ? 0 : (int)stockBox.Value,
                        isActive = activeBox.IsChecked == true
                    };

                    if (existing == null)
                        await ApiClient.PostAsync("products", body);
                    else
                        await ApiClient.PutAsync($"products/{existing.ProductId}", body);
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

        private void Show(InfoBarSeverity severity, string message)
        {
            MessageBar.Severity = severity;
            MessageBar.Message = message;
            MessageBar.IsOpen = true;
        }
    }
}