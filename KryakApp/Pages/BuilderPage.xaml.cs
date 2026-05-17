using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using System;
using System.Collections.ObjectModel;
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
    }
}
