using KryakApp.Pages;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinRT.Interop;
using Microsoft.UI.Windowing;


namespace KryakApp
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            this.ExtendsContentIntoTitleBar = true;
            this.SetTitleBar(titleBar);
            navView.SelectedItem = navView.MenuItems[0];
        }
        private void navView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is not NavigationViewItem item)
                return;

            string? tag = item.Tag?.ToString();

            switch (tag)
            {
                case "Server":
                    navFrame.Navigate(typeof(ServerPage));
                    break;

                case "Nodes":
                    navFrame.Navigate(typeof(NodesPage));
                    break;

                case "Builder":
                    navFrame.Navigate(typeof(BuilderPage));
                    break;

                case "Logs":
                    navFrame.Navigate(typeof(LogsPage));
                    break;

                case "Settings":
                    navFrame.Navigate(typeof(SettingsPage));
                    break;
            }
        }
        private void TitleBar_PaneToggleRequested(TitleBar sender, object args)
        {
            navView.IsPaneOpen = !navView.IsPaneOpen;
        }
        public void ApplyTitleBarTheme(ElementTheme theme)
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            var titleBar = appWindow.TitleBar;

            if (theme == ElementTheme.Light)
            {
                titleBar.ButtonForegroundColor = Colors.Black;
                titleBar.ButtonHoverForegroundColor = Colors.Black;
                titleBar.ButtonPressedForegroundColor = Colors.Black;

                titleBar.ButtonBackgroundColor = Colors.Transparent;
                titleBar.ButtonHoverBackgroundColor = ColorHelper.FromArgb(255, 230, 230, 230);
                titleBar.ButtonPressedBackgroundColor = ColorHelper.FromArgb(255, 210, 210, 210);
            }
            else
            {
                titleBar.ButtonForegroundColor = Colors.White;
                titleBar.ButtonHoverForegroundColor = Colors.White;
                titleBar.ButtonPressedForegroundColor = Colors.White;

                titleBar.ButtonBackgroundColor = Colors.Transparent;
                titleBar.ButtonHoverBackgroundColor = ColorHelper.FromArgb(255, 50, 50, 50);
                titleBar.ButtonPressedBackgroundColor = ColorHelper.FromArgb(255, 70, 70, 70);
            }
        }

        public void ApplyNavigationStyle(string style)
        {
            switch (style)
            {
                case "Left":
                    navView.PaneDisplayMode = NavigationViewPaneDisplayMode.Left;
                    navView.IsPaneToggleButtonVisible = false;
                    titleBar.IsPaneToggleButtonVisible = true;
                    break;

                case "Top":
                    navView.PaneDisplayMode = NavigationViewPaneDisplayMode.Top;
                    navView.IsPaneToggleButtonVisible = false;
                    titleBar.IsPaneToggleButtonVisible = false;
                    navView.IsPaneOpen = false;
                    break;
            }
        }
    }
}