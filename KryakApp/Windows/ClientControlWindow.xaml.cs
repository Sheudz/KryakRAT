using KryakApp.Services;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Threading.Tasks;
using WinRT.Interop;

namespace KryakApp.Windows;

public sealed partial class ClientControlWindow : Window
{
    private readonly UserData _user;

    public ClientControlWindow(UserData user)
    {
        InitializeComponent();
        _user = user;
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(WindowTitleBar);

        nint hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        AppWindow appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow.Resize(new global::Windows.Graphics.SizeInt32(420, 420));

        UpdateView();
        _user.PropertyChanged += User_PropertyChanged;
        Closed += ClientControlWindow_Closed;
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
        Title = $"Control - {_user.Username}";
        TagTextBlock.Text = _user.VictimTag;
        UserTextBlock.Text = _user.Username;
        IpTextBlock.Text = _user.UserIPAddress;
        OsTextBlock.Text = _user.UserOS;
        PingTextBlock.Text = string.IsNullOrWhiteSpace(_user.Ping) ? "-" : $"{_user.Ping} ms";
    }

    private void ClientControlWindow_Closed(object sender, WindowEventArgs args)
    {
        _user.PropertyChanged -= User_PropertyChanged;
        Closed -= ClientControlWindow_Closed;
    }

    private async void RestartClientButton_Click(object sender, RoutedEventArgs e)
    {
        await SendCommandAsync("restart_client");
    }

    private async void CloseClientButton_Click(object sender, RoutedEventArgs e)
    {
        await SendCommandAsync("close_client");
    }

    private async void DeleteClientButton_Click(object sender, RoutedEventArgs e)
    {
        await SendCommandAsync("delete_client");
    }

    private async Task SendCommandAsync(string command)
    {
        bool sent = await App.Server.SendClientCommandAsync(_user, command);
        if (sent)
        {
            return;
        }

        ContentDialog dialog = new()
        {
            Title = "Command failed",
            Content = "Unable to send command to client.",
            CloseButtonText = "OK"
        };

        if (Content is FrameworkElement root)
        {
            dialog.XamlRoot = root.XamlRoot;
        }

        _ = dialog.ShowAsync();
    }
}
