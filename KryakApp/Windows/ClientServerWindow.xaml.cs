using KryakApp.Services;
using Microsoft.UI.Xaml;

namespace KryakApp.Windows;

public sealed partial class ClientServerWindow : Window
{
    private readonly UserData _user;

    public ClientServerWindow(UserData user)
    {
        InitializeComponent();
        _user = user;

        UpdateView();
        _user.PropertyChanged += User_PropertyChanged;
        Closed += ClientServerWindow_Closed;
    }

    private void User_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (!DispatcherQueue.HasThreadAccess)
        {
            DispatcherQueue.TryEnqueue(UpdateView);
            return;
        }

        UpdateView();
    }

    private void UpdateView()
    {
        Title = $"Server - {_user.Username}";
        TagTextBlock.Text = $"Tag: {_user.VictimTag}";
        UserTextBlock.Text = $"Username: {_user.Username}";
        IpTextBlock.Text = $"IP: {_user.UserIPAddress}";
        OsTextBlock.Text = $"OS: {_user.UserOS}";
        PingTextBlock.Text = $"Ping: {_user.Ping} ms";
    }

    private void ClientServerWindow_Closed(object sender, WindowEventArgs args)
    {
        _user.PropertyChanged -= User_PropertyChanged;
        Closed -= ClientServerWindow_Closed;
    }
}
