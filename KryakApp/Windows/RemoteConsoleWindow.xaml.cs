using KryakApp.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WinRT.Interop;

namespace KryakApp.Windows;

public sealed partial class RemoteConsoleWindow : Window
{
    private readonly UserData _user;
    private readonly StringBuilder _pendingOutput = new();
    private readonly object _pendingLock = new();
    private readonly DispatcherQueueTimer _flushTimer;

    public RemoteConsoleWindow(UserData user)
    {
        InitializeComponent();
        _user = user;

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(WindowTitleBar);

        nint hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        AppWindow appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow.Resize(new global::Windows.Graphics.SizeInt32(760, 480));

        Title = $"Remote Console - {_user.Username}";
        AppendOutput($"Connected target: {_user.Username} ({_user.UserIPAddress})");

        App.Server.ConsoleOutputReceived += Server_ConsoleOutputReceived;
        Closed += RemoteConsoleWindow_Closed;

        _flushTimer = DispatcherQueue.CreateTimer();
        _flushTimer.Interval = TimeSpan.FromMilliseconds(120);
        _flushTimer.Tick += FlushTimer_Tick;
        _flushTimer.Start();
    }

    private void RemoteConsoleWindow_Closed(object sender, WindowEventArgs args)
    {
        _flushTimer.Stop();
        _flushTimer.Tick -= FlushTimer_Tick;
        App.Server.ConsoleOutputReceived -= Server_ConsoleOutputReceived;
        Closed -= RemoteConsoleWindow_Closed;
    }

    private void Server_ConsoleOutputReceived(UserData user, string output)
    {
        if (!ReferenceEquals(user, _user))
        {
            return;
        }

        lock (_pendingLock)
        {
            _pendingOutput.AppendLine(output);
        }
    }

    private void FlushTimer_Tick(DispatcherQueueTimer sender, object args)
    {
        string chunk;
        lock (_pendingLock)
        {
            if (_pendingOutput.Length == 0)
            {
                return;
            }

            chunk = _pendingOutput.ToString();
            _pendingOutput.Clear();
        }

        AppendOutput(chunk);
    }

    private async void SendButton_Click(object sender, RoutedEventArgs e)
    {
        await QueueCommandSendAsync();
    }

    private async void CommandTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != global::Windows.System.VirtualKey.Enter)
        {
            return;
        }

        e.Handled = true;
        await QueueCommandSendAsync();
    }

    private Task QueueCommandSendAsync()
    {
        string commandText = CommandTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(commandText))
        {
            return Task.CompletedTask;
        }

        CommandTextBox.Text = string.Empty;
        AppendOutput($"> {commandText}");

        _ = Task.Run(async () =>
        {
            using CancellationTokenSource cts = new(TimeSpan.FromSeconds(4));
            string payload = $"remote_console:{commandText}";
            bool sent = await App.Server.SendClientCommandAsync(_user, payload, cts.Token);
            if (sent)
            {
                return;
            }

            if (!DispatcherQueue.HasThreadAccess)
            {
                DispatcherQueue.TryEnqueue(() => AppendOutput("[error] failed to send command"));
                return;
            }

            AppendOutput("[error] failed to send command");
        });

        return Task.CompletedTask;
    }

    private void AppendOutput(string line)
    {
        const int maxBufferChars = 120_000;
        string existing = ConsoleOutputTextBox.Text;
        ConsoleOutputTextBox.Text = string.IsNullOrWhiteSpace(existing)
            ? line
            : $"{existing}{Environment.NewLine}{line}";

        if (ConsoleOutputTextBox.Text.Length > maxBufferChars)
        {
            ConsoleOutputTextBox.Text = ConsoleOutputTextBox.Text[^maxBufferChars..];
        }

        ConsoleOutputTextBox.Select(ConsoleOutputTextBox.Text.Length, 0);
    }
}
