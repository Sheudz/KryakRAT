using KryakApp.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace KryakApp
{
    public partial class App : Application
    {
        public static Server Server { get; } = new();
        public static ConnectedUsersStore ConnectedUsers { get; } = new();
        public static Window? MainWindow { get; private set; }

        private static readonly StringBuilder _log = new();
        private static readonly object _logLock = new();

        public static string GetLogText() { lock (_logLock) return _log.ToString(); }

        public static event Action<string>? Logged;

        private static void WriteLog(string message)
        {
            string entry = $"[{DateTime.Now:HH:mm:ss}] {message}";
            lock (_logLock)
            {
                _log.Append(entry);
                _log.Append('\n');
                if (_log.Length > 50000)
                    _log.Remove(0, _log.Length - 35000);
            }
            Logged?.Invoke(entry);
        }

        public App()
        {
            InitializeComponent();
            Server.UserConnected += Server_UserConnected;
            Server.UserPingUpdated += Server_UserPingUpdated;
            Server.UserDisconnected += Server_UserDisconnected;
            Server.ServerStateChanged += Server_ServerStateChanged;
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            MainWindow = new MainWindow();
            MainWindow.Activate();
        }

        private static void Server_ServerStateChanged(bool running)
        {
            if (running)
                WriteLog($"Server started on port {Server.Port}");
            else
                WriteLog("Server stopped");
        }

        private static void Server_UserConnected(UserData userData)
        {
            WriteLog($"Client connected: {userData.Username} ({userData.UserIPAddress})");

            if (MainWindow?.DispatcherQueue == null)
                return;

            MainWindow.DispatcherQueue.TryEnqueue(() =>
            {
                ConnectedUsers.Add(userData);
            });
        }

        private static void Server_UserPingUpdated(UserData userData, string ping)
        {
            if (MainWindow?.DispatcherQueue == null)
                return;

            MainWindow.DispatcherQueue.TryEnqueue(() =>
            {
                userData.Ping = ping;
            });
        }

        private static void Server_UserDisconnected(UserData userData)
        {
            WriteLog($"Client disconnected: {userData.Username} ({userData.UserIPAddress})");

            if (MainWindow?.DispatcherQueue == null)
                return;

            MainWindow.DispatcherQueue.TryEnqueue(() =>
            {
                ConnectedUsers.Remove(userData);
            });
        }
    }
}
