using KryakApp.Controls;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.ObjectModel;

namespace KryakApp.Pages
{
    public sealed partial class NodesPage : Page
    {
        public NodesPage()
        {
            InitializeComponent();

            SetupGrid();
            LoadTestUsers();
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
        }

        private void LoadTestUsers()
        {
            for (int i = 1; i <= 21; i++)
            {
                PeopleGrid.Items.Add(new UserData
                {
                    UserIPAddress = "123.45.67.8",
                    VictimTag = "AllahSvinka",
                    Username = i.ToString(),
                    Country = "Hohlostan",
                    UserOS = "Swindows 9",
                    AdminStatus = true,
                    CameraStatus = false,
                    MicrophoneStatus = true,
                    Ping = "67",
                    Client = "test"
                });
            }
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

            MenuFlyoutItem ManagerItem = new() { Text = "Manager" };
            ManagerItem.Click += (_, _) => { Manager(row.Client); };
            MenuFlyoutItem RemoteDesktopItem = new() { Text = "Remote desktop" };
            RemoteDesktopItem.Click += (_, _) => { RemoteDesktop(row.Client); };
            MenuFlyoutItem RemoteConsoleItem = new() { Text = "Remote console" };
            RemoteConsoleItem.Click += (_, _) => { RemoteConsole(row.Client); };
            MenuFlyoutItem RunFileItem = new() { Text = "Run file" };
            RunFileItem.Click += (_, _) => { RunFile(row.Client); };
            MenuFlyoutItem ServerItem = new() { Text = "Server" };
            ServerItem.Click += (_, _) => { Server(row.Client); };

            menu.Items.Add(ManagerItem);
            menu.Items.Add(RemoteDesktopItem);
            menu.Items.Add(RemoteConsoleItem);
            menu.Items.Add(RunFileItem);
            menu.Items.Add(ServerItem);

            return menu;
        }

        private void Manager(string username)
        {
            
        }
        private void RemoteDesktop(string username)
        {

        }
        private void RemoteConsole(string username)
        {

        }
        private void RunFile(string username)
        {

        }
        private void Server(string username)
        {

        }
    }
}