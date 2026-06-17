using KryakApp.Services;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.IO;
using System.Threading.Tasks;
using WinRT.Interop;

namespace KryakApp.Windows;

public sealed partial class RemoteDesktopWindow : Window
{
    private readonly UserData _user;
    private bool _isStreaming;
    private bool _suppressEvents;
    private int _lastFrameWidth;
    private int _lastFrameHeight;

    public RemoteDesktopWindow(UserData user)
    {
        InitializeComponent();
        _user = user;

        _isStreaming = false;
        _suppressEvents = true;

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(WindowTitleBar);

        nint hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        AppWindow appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow.Resize(new global::Windows.Graphics.SizeInt32(820, 600));

        Title = $"Remote Desktop - {_user.Username}";
        PopulateMonitors();

        _suppressEvents = false;

        _user.PropertyChanged += User_PropertyChanged;
        App.Server.DesktopFrameReceived += Server_DesktopFrameReceived;
        Closed += RemoteDesktopWindow_Closed;
    }

    private void RemoteDesktopWindow_Closed(object sender, WindowEventArgs args)
    {
        if (_isStreaming)
        {
            _ = App.Server.SendDesktopStopAsync(_user);
        }

        App.Server.DesktopFrameReceived -= Server_DesktopFrameReceived;
        _user.PropertyChanged -= User_PropertyChanged;
        Closed -= RemoteDesktopWindow_Closed;
    }

    private void Server_DesktopFrameReceived(UserData user, int monitor, int quality, string frameData)
    {
        if (!ReferenceEquals(user, _user))
        {
            return;
        }

        if (!DispatcherQueue.HasThreadAccess)
        {
            DispatcherQueue.TryEnqueue(() => SetDesktopFrame(frameData));
            return;
        }

        SetDesktopFrame(frameData);
    }

    private void SetDesktopFrame(string base64Data)
    {
        try
        {
            byte[] bytes = Convert.FromBase64String(base64Data);
            using MemoryStream ms = new(bytes);
            BitmapImage bitmap = new();
            bitmap.SetSource(ms.AsRandomAccessStream());
            DesktopImage.Source = bitmap;
            _lastFrameWidth = bitmap.PixelWidth;
            _lastFrameHeight = bitmap.PixelHeight;
        }
        catch
        {
        }
    }

    private async void DesktopImage_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (!_isStreaming || MouseControlCheckBox.IsChecked != true)
            return;

        var point = e.GetCurrentPoint(DesktopImage);
        double clickX = point.Position.X;
        double clickY = point.Position.Y;

        if (_lastFrameWidth <= 0 || _lastFrameHeight <= 0)
            return;

        double renderedWidth = DesktopImage.ActualWidth;
        double renderedHeight = DesktopImage.ActualHeight;

        double scaleX = renderedWidth / _lastFrameWidth;
        double scaleY = renderedHeight / _lastFrameHeight;
        double scale = Math.Min(scaleX, scaleY);

        double contentWidth = _lastFrameWidth * scale;
        double contentHeight = _lastFrameHeight * scale;
        double offsetX = (renderedWidth - contentWidth) / 2;
        double offsetY = (renderedHeight - contentHeight) / 2;

        int imageX = (int)((clickX - offsetX) / scale);
        int imageY = (int)((clickY - offsetY) / scale);

        if (imageX < 0 || imageX >= _lastFrameWidth || imageY < 0 || imageY >= _lastFrameHeight)
            return;

        string button = point.Properties.IsRightButtonPressed ? "right" : "left";
        await App.Server.SendMouseClickAsync(_user, imageX, imageY, button);
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
        _suppressEvents = true;

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

        _suppressEvents = false;
    }

    private void MonitorCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressEvents)
        {
            return;
        }

        if (_isStreaming)
        {
            RestartStreamWithCurrentSettings();
        }
    }

    private void QualityCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressEvents)
        {
            return;
        }

        if (_isStreaming)
        {
            RestartStreamWithCurrentSettings();
        }
    }

    private int GetSelectedMonitor()
    {
        if (MonitorCombo.SelectedItem is ComboBoxItem monitorItem && monitorItem.Tag is int mi)
        {
            return mi;
        }

        return 0;
    }

    private int GetSelectedQuality()
    {
        if (QualityCombo.SelectedItem is ComboBoxItem qualityItem && qualityItem.Tag is string tagStr && int.TryParse(tagStr, out int qi))
        {
            return qi;
        }

        return 50;
    }

    private async void RestartStreamWithCurrentSettings()
    {
        int monitorIndex = GetSelectedMonitor();
        int quality = GetSelectedQuality();
        await App.Server.SendDesktopStartAsync(_user, monitorIndex, quality);
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        StartButton.IsEnabled = false;

        if (_isStreaming)
        {
            bool sent = await App.Server.SendDesktopStopAsync(_user);
            if (sent)
            {
                _isStreaming = false;
                StartButton.Content = "Start";
                StartButton.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
            }
        }
        else
        {
            int monitorIndex = GetSelectedMonitor();
            int quality = GetSelectedQuality();
            bool sent = await App.Server.SendDesktopStartAsync(_user, monitorIndex, quality);
            if (sent)
            {
                _isStreaming = true;
                StartButton.Content = "Stop";
            }
        }

        StartButton.IsEnabled = true;
    }
}
