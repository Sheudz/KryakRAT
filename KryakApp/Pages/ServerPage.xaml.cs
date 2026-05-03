using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using WinRT.Interop;
using System.IO;

namespace KryakApp.Pages;

public sealed partial class ServerPage : Page
{
    private DispatcherTimer _uptimeTimer = new();
    private DateTime _startTime;
    public ServerPage()
    {
        InitializeComponent();
    }

    private async void StartServerButton_Click(object sender, RoutedEventArgs e)
    {
        int port = (int)PortNumberBox.Value;
        string certificatePath = CertificatePathTextBox.Text;
        string certificatePassword = CertificatePasswordBox.Password;

        if (App.Server.IsRunning)
        {
            await ShowErrorAsync("Server is already running.", "Error");
            return;
        }

        if (!Validation.IsValidPort(port, out string portError))
        {
            await ShowErrorAsync(portError, "Invalid Port");
            return;
        }

        if (!Validation.IsValidCertificatePath(certificatePath, out string pathError))
        {
            await ShowErrorAsync(pathError, "Invalid Certificate Path");
            return;
        }

        if (string.IsNullOrWhiteSpace(certificatePassword))
        {
            await ShowErrorAsync("Certificate password cannot be empty.", "Invalid Password");
            return;
        }

        if (!Validation.TryLoadServerCertificate(certificatePath, certificatePassword, out var cert, out string certError))
        {
            await ShowErrorAsync(certError, "Certificate Error");
            return;
        }

        try
        {
            await App.Server.StartServer(port, certificatePath, certificatePassword);
            _startTime = DateTime.UtcNow;

            _uptimeTimer.Interval = TimeSpan.FromSeconds(1);
            _uptimeTimer.Tick -= UptimeTimer_Tick;
            _uptimeTimer.Tick += UptimeTimer_Tick;
            _uptimeTimer.Start();
            SetServerOnline(port);
        }
        catch (Exception ex)
        {
            SetServerOffline();
            await ShowErrorAsync(ex.Message, "Server start failed");
        }
    }
    private void UptimeTimer_Tick(object sender, object e)
    {
        var uptime = DateTime.UtcNow - _startTime;

        UptimeText.Text = $"{uptime.Hours:D2}:{uptime.Minutes:D2}";
    }

    private async void StopServerButton_Click(object sender, RoutedEventArgs e)
    {
        await App.Server.StopServer();
        _uptimeTimer.Stop();
        _uptimeTimer.Tick -= UptimeTimer_Tick;
        SetServerOffline();
    }

    private void GenerateCertificateButton_Click(object sender, RoutedEventArgs e)
    {
        string password = "kryak1337";
        string outputPath = Path.Combine(AppContext.BaseDirectory, "KryakCertificate.pfx");

        using RSA rsa = RSA.Create(2048);

        CertificateRequest request = new(
            "CN=KryakServer",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature |
                X509KeyUsageFlags.KeyEncipherment,
                false));

        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(
                new OidCollection
                {
                new Oid("1.3.6.1.5.5.7.3.1")
                },
                false));

        using X509Certificate2 cert = request.CreateSelfSigned(
            DateTimeOffset.Now.AddMinutes(-1),
            DateTimeOffset.Now.AddYears(1));

        byte[] pfxBytes = cert.Export(X509ContentType.Pfx, password);

        File.WriteAllBytes(outputPath, pfxBytes);

        CertificatePasswordBox.Password = password;
        CertificatePathTextBox.Text = outputPath;
    }

    private void SetServerOnline(int port)
    {
        ServerStateText.Text = "Online";
        ServerStateText.Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 131, 215, 101));
        PortText.Text = port.ToString();
        UptimeText.Text = "00:00";
    }

    private void SetServerOffline()
    {
        ServerStateText.Text = "Offline";
        ServerStateText.Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 224, 108, 117));
        PortText.Text = "None";
        UptimeText.Text = "00:00";
        UsersText.Text = "0";
    }

    private async void BrowseCertificateButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();

        var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
        InitializeWithWindow.Initialize(picker, hwnd);

        picker.ViewMode = PickerViewMode.List;
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;

        picker.FileTypeFilter.Add(".pfx");
        picker.FileTypeFilter.Add(".p12");

        StorageFile file = await picker.PickSingleFileAsync();

        if (file != null)
        {
            CertificatePathTextBox.Text = file.Path;
        }
    }

    private async Task ShowErrorAsync(string message, string title)
    {
        ContentDialog dialog = new()
        {
            Title = title,
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = this.XamlRoot
        };

        await dialog.ShowAsync();
    }
}