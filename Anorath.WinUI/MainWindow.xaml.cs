using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace Anorath.WinUI
{
    public sealed partial class MainWindow : Window
    {
        // Lets any page ask the window to switch pages
        public static MainWindow? Current { get; private set; }

        public MainWindow()
        {
            InitializeComponent();
            Current = this;
            AppWindow.Resize(new Windows.Graphics.SizeInt32(1200, 800));

            RootFrame.Navigate(typeof(Pages.LoginPage));
        }

        public void NavigateTo(Type page) => RootFrame.Navigate(page);
    }
}