using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace KryakApp
{
    public sealed partial class SettingsPage : Page
    {
        public SettingsPage()
        {
            InitializeComponent();
        }

        private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ThemeComboBox.SelectedItem is not ComboBoxItem item)
                return;

            var window = App.MainWindow;
            if (window?.Content is not FrameworkElement root)
                return;

            switch (item.Content?.ToString())
            {
                case "Light":
                    root.RequestedTheme = ElementTheme.Light;
                    ((MainWindow)App.MainWindow!).ApplyTitleBarTheme(ElementTheme.Light);
                    break;

                case "Dark":
                    root.RequestedTheme = ElementTheme.Dark;
                    ((MainWindow)App.MainWindow!).ApplyTitleBarTheme(ElementTheme.Dark);
                    break;

                case "System":
                    root.RequestedTheme = ElementTheme.Default;
                    ((MainWindow)App.MainWindow!).ApplyTitleBarTheme(ElementTheme.Dark);
                    break;
            }
        }

        private void NavigationStyleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (NavigationStyleComboBox.SelectedItem is not ComboBoxItem item)
                return;

            string? style = item.Content?.ToString();

            if (style is null)
                return;

            if (App.MainWindow is MainWindow mainWindow)
                mainWindow.ApplyNavigationStyle(style);
        }

        private void SoundToggle_Toggled(object sender, RoutedEventArgs e)
        {
            SoundStateText.Text = SoundToggle.IsOn ? "On" : "Off";

            ElementSoundPlayer.State = SoundToggle.IsOn
                ? ElementSoundPlayerState.On
                : ElementSoundPlayerState.Off;
        }
    }
}