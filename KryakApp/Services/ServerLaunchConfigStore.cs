using System;
using System.IO;
using System.Text.Json;

namespace KryakApp.Services;

public sealed class ServerLaunchSettings
{
    public int Port { get; set; }
    public string CertificatePath { get; set; } = string.Empty;
    public string CertificatePassword { get; set; } = string.Empty;
}

public static class ServerLaunchConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static readonly string ConfigFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "KryakApp",
        "server-launch.json");

    public static bool TryLoad(out ServerLaunchSettings settings)
    {
        settings = new ServerLaunchSettings();

        try
        {
            if (!File.Exists(ConfigFilePath))
            {
                return false;
            }

            string json = File.ReadAllText(ConfigFilePath);
            ServerLaunchSettings? parsed = JsonSerializer.Deserialize<ServerLaunchSettings>(json);
            if (parsed == null)
            {
                return false;
            }

            settings = parsed;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static void Save(ServerLaunchSettings settings)
    {
        string? dir = Path.GetDirectoryName(ConfigFilePath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        string json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(ConfigFilePath, json);
    }
}
