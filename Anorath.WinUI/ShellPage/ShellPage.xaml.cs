using System;
using System.Collections.Generic;
using System.Linq;
using Anorath.WinUI.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Anorath.WinUI.Pages
{
    public sealed partial class ShellPage : Page
    {
        // Menu Tag -> page. Remove the // on a line once that page is built.
        private readonly Dictionary<string, Type> _pages = new()
        {
            { "SupplyChain", typeof(SupplyChainPage) },
            { "Menu", typeof(MenuPage) },
            { "POS", typeof(PosPage) },
            { "Reservations", typeof(ReservationsPage) },
            { "Customers", typeof(CustomersPage) },
            { "Dashboard", typeof(DashboardPage) },
            { "Rooms", typeof(RoomsPage) },
            { "Users", typeof(UsersPage) },
        };

        // What each subscription PLAN includes (ERP modules per tier)
        private static readonly Dictionary<string, string[]> PlanMenus = new()
        {
            ["Micro"] = new[] { "Rooms", "Reservations", "Customers", "Menu", "POS", "Reports", "Users" },
            ["Small"] = new[] { "Dashboard", "Rooms", "Reservations", "Customers", "Menu", "POS", "Reports",
                                 "SupplyChain", "Payroll", "Financials", "Users" },
            ["Medium"] = new[] { "Dashboard", "Rooms", "Reservations", "Customers", "Menu", "POS", "Reports",
                                 "SupplyChain", "Payroll", "Financials", "Branches", "Users" },
        };

        // What each ROLE is allowed to open. "*" = everything in the plan.
        private static readonly Dictionary<string, string[]> RoleMenus = new()
        {
            ["Admin"] = new[] { "*" },
            ["Manager"] = new[] { "Dashboard", "SupplyChain", "Payroll", "Reports" },
            ["BranchManager"] = new[] { "Dashboard", "SupplyChain", "Payroll", "Reports" },
            ["Receptionist"] = new[] { "Rooms", "Reservations", "Customers" },
            ["Cashier"] = new[] { "Menu", "POS" },
        };

        public ShellPage()
        {
            InitializeComponent();
            Loaded += (s, e) => ApplyMenu();
        }

        // Visible menu = what the PLAN includes AND what the ROLE may open
        private HashSet<string> GetAllowedTags()
        {
            var planTags = PlanMenus.TryGetValue(Session.Plan, out var p) ? p : Array.Empty<string>();
            var roleTags = RoleMenus.TryGetValue(Session.Role, out var r) ? r : Array.Empty<string>();

            return roleTags.Contains("*")
                ? new HashSet<string>(planTags)
                : new HashSet<string>(planTags.Intersect(roleTags));
        }

        private void ApplyMenu()
        {
            NavView.PaneTitle = $"{Session.CompanyName} ({Session.Plan})";

            var allowed = GetAllowedTags();

            NavigationViewItem? first = null;
            NavigationViewItemHeader? currentHeader = null;
            var headerHasItems = new Dictionary<NavigationViewItemHeader, bool>();

            foreach (var obj in NavView.MenuItems)
            {
                if (obj is NavigationViewItemHeader header)
                {
                    currentHeader = header;
                    headerHasItems[header] = false;
                    continue;
                }

                if (obj is not NavigationViewItem item) continue;

                var show = allowed.Contains(item.Tag?.ToString() ?? "");
                item.Visibility = show ? Visibility.Visible : Visibility.Collapsed;

                if (show)
                {
                    first ??= item;
                    if (currentHeader != null) headerHasItems[currentHeader] = true;
                }
            }

            // Hide section headers with nothing visible under them
            foreach (var pair in headerHasItems)
                pair.Key.Visibility = pair.Value ? Visibility.Visible : Visibility.Collapsed;

            if (first != null)
                NavView.SelectedItem = first;
        }

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is not NavigationViewItem item) return;
            var tag = item.Tag?.ToString() ?? "";

            if (tag == "Logout")
            {
                Session.Clear();
                MainWindow.Current?.NavigateTo(typeof(LoginPage));
                return;
            }

            NavView.Header = item.Content;

            if (_pages.TryGetValue(tag, out var pageType))
                ContentFrame.Navigate(pageType);
            else
                ContentFrame.Content = new TextBlock { Text = "This screen is coming next.", Opacity = 0.6, FontSize = 16 };
        }
    }
}