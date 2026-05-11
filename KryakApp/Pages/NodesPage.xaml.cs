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

            PeopleGrid.RowRightClick += (s, e) =>
            {
                if (e.RowItem is UserData row)
                {
                    ContentDialog dialog = new()
                    {
                        Title = "test",
                        Content = row.Username,
                        CloseButtonText = "OK",
                        XamlRoot = this.XamlRoot
                    };

                    dialog.ShowAsync();
                }
            };
        }
    }
}