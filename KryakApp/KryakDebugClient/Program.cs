using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Security.Authentication;
using System.Text;
using System.Text.Json;

const int defaultPort = 5555;

int port = defaultPort;
if (args.Length > 0 && !int.TryParse(args[0], out port))
{
    Console.WriteLine("Invalid port. Usage: dotnet run --project KryakDebugClient [port]");
    return;
}

UserPayload payload = new()
{
    UserIPAddress = "",
    VictimTag = "DebugClient",
    Username = Environment.UserName,
    Country = "Local",
    UserOS = Environment.OSVersion.VersionString,
    AdminStatus = false,
    CameraStatus = false,
    MicrophoneStatus = false,
    Ping = "0"
};

string json = JsonSerializer.Serialize(payload);

SslClientAuthenticationOptions sslOptions = new()
{
    ApplicationProtocols = [new SslApplicationProtocol("kryak")],
    EnabledSslProtocols = SslProtocols.Tls13,
    RemoteCertificateValidationCallback = static (_, _, _, _) => true,
    TargetHost = "localhost"
};

QuicClientConnectionOptions options = new()
{
    RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, port),
    ClientAuthenticationOptions = sslOptions,
    DefaultCloseErrorCode = 0,
    DefaultStreamErrorCode = 0
};

Console.WriteLine($"Connecting to 127.0.0.1:{port}...");

await using QuicConnection connection = await QuicConnection.ConnectAsync(options);
await using QuicStream stream = await connection.OpenOutboundStreamAsync(QuicStreamType.Unidirectional);

byte[] bytes = Encoding.UTF8.GetBytes(json);
await stream.WriteAsync(bytes);

Console.WriteLine("Payload sent successfully:");
Console.WriteLine(json);

public sealed class UserPayload
{
    public string UserIPAddress { get; set; } = string.Empty;
    public string VictimTag { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string UserOS { get; set; } = string.Empty;
    public bool AdminStatus { get; set; }
    public bool CameraStatus { get; set; }
    public bool MicrophoneStatus { get; set; }
    public string Ping { get; set; } = string.Empty;
}
