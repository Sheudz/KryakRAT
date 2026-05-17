using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace KryakApp.Pages
{
    public sealed partial class BuilderPage : Page
    {
        private readonly ObservableCollection<string> _connections = [];

        public BuilderPage()
        {
            InitializeComponent();
            ConnectionListView.ItemsSource = _connections;
            UpdateConnectionCount();

            Loaded += BuilderPage_Loaded;
            Unloaded += BuilderPage_Unloaded;
        }

        private void BuilderPage_Loaded(object sender, RoutedEventArgs e)
        {
            App.Server.ServerStateChanged += Server_ServerStateChanged;
            UpdateTrustButtonState();
        }

        private void BuilderPage_Unloaded(object sender, RoutedEventArgs e)
        {
            App.Server.ServerStateChanged -= Server_ServerStateChanged;
        }

        private void Server_ServerStateChanged(bool isRunning)
        {
            if (!DispatcherQueue.HasThreadAccess)
            {
                DispatcherQueue.TryEnqueue(UpdateTrustButtonState);
                return;
            }

            UpdateTrustButtonState();
        }

        private void ConnectionModeRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (IpPortInputPanel == null || RawInputPanel == null)
            {
                return;
            }

            bool rawMode = sender is RadioButton radio &&
                           string.Equals(radio.Name, nameof(RawModeRadio), StringComparison.Ordinal);

            IpPortInputPanel.Visibility = rawMode ? Visibility.Collapsed : Visibility.Visible;
            RawInputPanel.Visibility = rawMode ? Visibility.Visible : Visibility.Collapsed;
        }

        private void AddConnectionButton_Click(object sender, RoutedEventArgs e)
        {
            string? item = null;

            if (RawModeRadio.IsChecked == true)
            {
                string raw = RawUrlTextBox.Text.Trim();
                if (string.IsNullOrWhiteSpace(raw))
                {
                    return;
                }

                item = raw;
                RawUrlTextBox.Text = string.Empty;
            }
            else
            {
                string ip = IpTextBox.Text.Trim();
                string port = PortTextBox.Text.Trim();
                if (string.IsNullOrWhiteSpace(ip) || string.IsNullOrWhiteSpace(port))
                {
                    return;
                }

                item = $"{ip}:{port}";
                IpTextBox.Text = string.Empty;
                PortTextBox.Text = string.Empty;
            }

            foreach (string existing in _connections)
            {
                if (string.Equals(existing, item, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            _connections.Add(item);
            UpdateConnectionCount();
        }

        private void RemoveConnectionButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not string value)
            {
                return;
            }

            _connections.Remove(value);
            UpdateConnectionCount();
        }

        private void CustomIconCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            IconPathTextBox.IsEnabled = true;
            BrowseIconButton.IsEnabled = true;
        }

        private void CustomIconCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            IconPathTextBox.IsEnabled = false;
            BrowseIconButton.IsEnabled = false;
        }

        private void DropCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            FileNameTextBox.IsEnabled = true;
            DropDirectoryTextBox.IsEnabled = true;
        }

        private void DropCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            FileNameTextBox.IsEnabled = false;
            DropDirectoryTextBox.IsEnabled = false;
        }

        private void PinnedModeRadio_Checked(object sender, RoutedEventArgs e)
        {
            PinnedOptionsPanel.Visibility = Visibility.Visible;
            UpdateTrustButtonState();
        }

        private void PinnedModeRadio_Unchecked(object sender, RoutedEventArgs e)
        {
            PinnedOptionsPanel.Visibility = Visibility.Collapsed;
        }

        private void TrustCurrentCertificateButton_Click(object sender, RoutedEventArgs e)
        {
            if (!App.Server.IsRunning)
            {
                return;
            }

            FingerprintTextBox.Text = App.Server.CurrentCertificateFingerprint;
        }

        private async void BrowseIconButton_Click(object sender, RoutedEventArgs e)
        {
            if (App.MainWindow is null)
            {
                return;
            }

            FileOpenPicker picker = new();
            nint hwnd = WindowNative.GetWindowHandle(App.MainWindow);
            InitializeWithWindow.Initialize(picker, hwnd);
            picker.SuggestedStartLocation = PickerLocationId.ComputerFolder;
            picker.FileTypeFilter.Add(".ico");
            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".jpg");

            StorageFile? file = await picker.PickSingleFileAsync();
            if (file is null)
            {
                return;
            }

            IconPathTextBox.Text = file.Path;
        }

        private void UpdateConnectionCount()
        {
            ConnectionCountText.Text = _connections.Count == 0
                ? "No endpoints added"
                : $"Endpoints: {_connections.Count}";
        }

        private void UpdateTrustButtonState()
        {
            TrustCurrentCertificateButton.IsEnabled = App.Server.IsRunning && PinnedModeRadio.IsChecked == true;
        }

        private async void BuildButton_Click(object sender, RoutedEventArgs e)
        {
            bool? hasGo = null;

            try
            {
                System.Diagnostics.Process.Start("go", "version")?.Kill();
                hasGo = true;
            }
            catch
            {
                hasGo = System.IO.File.Exists(System.IO.Path.Combine(GetPackagedLocalPath(), "go", "bin", "go.exe"));
            }
            if (hasGo == false)
            {
                ContentDialog dialog = new()
                {
                    Title = "Go Not Found",
                    Content = "Go compiler is required to build the client.\n\nYou can install Go manually or use the automatic installer.",
                    XamlRoot = XamlRoot,

                    PrimaryButtonText = "Install Automatically",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Primary
                };

                ContentDialogResult result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    bool installed = await InstallGoWithProgressAsync();
                }
                return;
            }
            if (_connections.Count == 0)
            {
                ContentDialog dialog = new()
                {
                    Title = "No Endpoints",
                    Content = "Please add at least one connection endpoint before building the client.",
                    XamlRoot = XamlRoot,
                };
                dialog.PrimaryButtonText = "OK";
                _ = dialog.ShowAsync();
                return;
            }
            if (PinnedModeRadio.IsChecked == true && string.IsNullOrWhiteSpace(FingerprintTextBox.Text))
            {
                ContentDialog dialog = new()
                {
                    Title = "No Certificate Fingerprint",
                    Content = "Please trust the current server certificate or enter a fingerprint manually.",
                    XamlRoot = XamlRoot,
                };
                dialog.PrimaryButtonText = "OK";
                _ = dialog.ShowAsync();
                return;
            }
            if (CustomIconCheckBox.IsChecked == true && System.IO.File.Exists(IconPathTextBox.Text))
            {
                ContentDialog dialog = new()
                {
                    Title = "No Icon Path",
                    Content = "Please select an icon file or uncheck the custom icon option.",
                    XamlRoot = XamlRoot,
                };
                dialog.PrimaryButtonText = "OK";
                _ = dialog.ShowAsync();
                return;
            }
            if (DropCheckBox.IsChecked == true && (string.IsNullOrWhiteSpace(FileNameTextBox.Text) || string.IsNullOrWhiteSpace(DropDirectoryTextBox.Text)))
            {
                ContentDialog dialog = new()
                {
                    Title = "Invalid Drop Settings",
                    Content = "Please enter a valid file name and drop directory or uncheck the drop option.",
                    XamlRoot = XamlRoot,
                };
                dialog.PrimaryButtonText = "OK";
                _ = dialog.ShowAsync();
                return;
            }
            string goPath;
            if (System.IO.File.Exists(System.IO.Path.Combine(GetPackagedLocalPath(), "go", "bin", "go.exe")))
            {
                goPath = System.IO.Path.Combine(GetPackagedLocalPath(), "go", "bin", "go.exe");
            }
            else            
            {
                goPath = "go";
            }
            
        }

        private static string GetPackagedLocalPath()
        {
            return System.IO.Path.Combine(ApplicationData.Current.LocalCacheFolder.Path, "Local", "KryakApp");
        }

        private async Task<bool> InstallGoWithProgressAsync()
        {
            TextBlock statusText = new()
            {
                Text = "Downloading Go compiler...",
                TextWrapping = TextWrapping.WrapWholeWords
            };

            ProgressBar progressBar = new()
            {
                IsIndeterminate = true,
                Height = 6
            };

            ContentDialog progressDialog = new()
            {
                Title = "Installing Go",
                XamlRoot = XamlRoot,
                IsPrimaryButtonEnabled = false,
                IsSecondaryButtonEnabled = false,
                CloseButtonText = string.Empty,
                Content = new StackPanel
                {
                    Spacing = 12,
                    Children =
                    {
                        statusText,
                        progressBar
                    }
                }
            };

            _ = progressDialog.ShowAsync();
            await Task.Delay(50);

            string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "go1.26.3.windows-amd64.zip");
            string extractPath = GetPackagedLocalPath();

            try
            {
                using System.Net.Http.HttpClient client = new();
                byte[] data = await client.GetByteArrayAsync("https://go.dev/dl/go1.26.3.windows-amd64.zip");
                await System.IO.File.WriteAllBytesAsync(tempPath, data);

                statusText.Text = "Extracting files...";

                if (System.IO.Directory.Exists(extractPath))
                {
                    System.IO.Directory.Delete(extractPath, recursive: true);
                }

                System.IO.Directory.CreateDirectory(extractPath);
                System.IO.Compression.ZipFile.ExtractToDirectory(tempPath, extractPath);
                System.IO.File.Delete(tempPath);
                return true;
            }
            catch
            {
                await ShowSimpleDialogAsync(
                    "Installation Failed",
                    "Failed to install Go compiler automatically. Please check internet connection and disk access, then try again.");
                return false;
            }
            finally
            {
                progressDialog.Hide();
            }
        }

        private async Task ShowSimpleDialogAsync(string title, string content)
        {
            ContentDialog dialog = new()
            {
                Title = title,
                Content = content,
                XamlRoot = XamlRoot,
                PrimaryButtonText = "OK"
            };

            await dialog.ShowAsync();
        }
    }
}
