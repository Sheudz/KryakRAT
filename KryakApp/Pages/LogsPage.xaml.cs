using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Text;

namespace KryakApp.Pages;

public sealed partial class LogsPage : Page
{
    private readonly StringBuilder _log = new();

    public LogsPage()
    {
        InitializeComponent();

        LogTextBox.Text = App.GetLogText();
        _log.Append(App.GetLogText());

        App.Logged += OnLogged;
    }

    private void OnLogged(string entry)
    {
        if (!DispatcherQueue.HasThreadAccess)
        {
            _ = DispatcherQueue.TryEnqueue(() => OnLogged(entry));
            return;
        }

        _log.Append(entry);
        _log.Append('\n');

        const int maxChars = 50000;
        if (_log.Length > maxChars)
        {
            _log.Remove(0, _log.Length - maxChars);
        }

        LogTextBox.Text = _log.ToString();
        LogScrollViewer.ChangeView(null, LogScrollViewer.ScrollableHeight, null);
    }
}
