using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;

namespace KryakApp.Services;

public static class DialogHelper
{
    public static async Task ShowCopyableAsync(XamlRoot xamlRoot, string title, string message)
    {
        TextBox textBox = new()
        {
            Text = message,
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = true,
            MinWidth = 360,
            MaxHeight = 300
        };

        ScrollViewer scrollViewer = new()
        {
            Content = textBox,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        ContentDialog dialog = new()
        {
            Title = title,
            Content = scrollViewer,
            XamlRoot = xamlRoot,
            CloseButtonText = "OK"
        };

        await dialog.ShowAsync();
    }
}