using KryakApp.Pages;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Runtime.InteropServices;
using Windows.UI.ViewManagement;
using WinRT.Interop;


namespace KryakApp
{
    public sealed partial class MainWindow : Window
    {
        private const int MinWindowWidth = 950;
        private const int MinWindowHeight = 600;
        private const int WmGetMinMaxInfo = 0x0024;
        private const int GwlWndProc = -4;
        private readonly nint _windowHandle;
        private readonly WndProcDelegate _wndProcDelegate;
        private readonly nint _previousWndProc;

        public MainWindow()
        {
            InitializeComponent();
            this.ExtendsContentIntoTitleBar = true;
            this.SetTitleBar(titleBar);

            _windowHandle = WindowNative.GetWindowHandle(this);
            _wndProcDelegate = WindowProc;
            nint wndProcPtr = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate);
            _previousWndProc = SetWindowLongPtr(_windowHandle, GwlWndProc, wndProcPtr);

            navView.SelectedItem = navView.MenuItems[0];
        }

        private nint WindowProc(nint hWnd, uint msg, nint wParam, nint lParam)
        {
            if (msg == WmGetMinMaxInfo)
            {
                MINMAXINFO minMaxInfo = Marshal.PtrToStructure<MINMAXINFO>(lParam);
                minMaxInfo.ptMinTrackSize.x = MinWindowWidth;
                minMaxInfo.ptMinTrackSize.y = MinWindowHeight;
                Marshal.StructureToPtr(minMaxInfo, lParam, fDeleteOld: false);
            }

            return CallWindowProc(_previousWndProc, hWnd, msg, wParam, lParam);
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

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }

        private delegate nint WndProcDelegate(nint hWnd, uint msg, nint wParam, nint lParam);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
        private static extern nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern nint CallWindowProc(nint lpPrevWndFunc, nint hWnd, uint msg, nint wParam, nint lParam);
    }
}
