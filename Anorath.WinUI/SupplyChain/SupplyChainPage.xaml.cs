using Microsoft.UI.Xaml.Controls;

namespace Anorath.WinUI.Pages
{
    public sealed partial class SupplyChainPage : Page
    {
        public SupplyChainPage()
        {
            InitializeComponent();
            SuppliersFrame.Navigate(typeof(SuppliersPage));
            OrdersFrame.Navigate(typeof(PurchaseOrdersPage));
        }

        // Reload POs (and the supplier list) every time the Purchase Orders tab is opened
        private async void Tabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Tabs is null || OrdersFrame is null) return;
            if (Tabs.SelectedIndex == 1 && OrdersFrame.Content is PurchaseOrdersPage page)
                await page.RefreshAsync();
        }
    }
}