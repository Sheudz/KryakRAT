using KryakApp.Services;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using WinRT.Interop;

namespace KryakApp.Windows;

public sealed partial class RemoteDesktopWindow : Window
{
    private readonly UserData _user;
    private bool _isStreaming;

    public RemoteDesktopWindow(UserData user)
    {
        InitializeComponent();
        _user = user;

        _isStreaming = false;

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(WindowTitleBar);

        nint hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        AppWindow appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow.Resize(new global::Windows.Graphics.SizeInt32(820, 600));

        Title = $"Remote Desktop - {_user.Username}";
        PopulateMonitors();

        _user.PropertyChanged += User_PropertyChanged;
        Closed += RemoteDesktopWindow_Closed;
    }

    private void RemoteDesktopWindow_Closed(object sender, WindowEventArgs args)
    {
        _user.PropertyChanged -= User_PropertyChanged;
        Closed -= RemoteDesktopWindow_Closed;
    }

    private void User_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(UserData.MonitorCount))
        {
            return;
        }

        if (!DispatcherQueue.HasThreadAccess)
        {
            DispatcherQueue.TryEnqueue(PopulateMonitors);
            return;
        }

        PopulateMonitors();
    }

    private void PopulateMonitors()
    {
        MonitorCombo.Items.Clear();
        int count = Math.Max(1, _user.MonitorCount);
        for (int i = 1; i <= count; i++)
        {
            MonitorCombo.Items.Add(new ComboBoxItem
            {
                Content = $"Monitor {i}",
                Tag = i - 1
            });
        }

        if (MonitorCombo.Items.Count > 0)
        {
            MonitorCombo.SelectedIndex = 0;
        }
    }

    private void MonitorCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
    }

    private void QualityCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        StartButton.IsEnabled = false;

        if (_isStreaming)
        {
            bool sent = await App.Server.SendClientCommandAsync(_user, "remote_desktop:stop");
            if (sent)
            {
                _isStreaming = false;
                StartButton.Content = "Start";
                StartButton.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
            }
        }
        else
        {
            bool sent = await App.Server.SendClientCommandAsync(_user, "remote_desktop:start");
            if (sent)
            {
                _isStreaming = true;
                StartButton.Content = "Stop";
            }
        }

        StartButton.IsEnabled = true;
    }
}
