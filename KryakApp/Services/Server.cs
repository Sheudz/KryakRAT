using System;
using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace KryakApp.Services;

public sealed class Server
{
    private QuicListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _listenerTask;

    public bool IsRunning { get; private set; }

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
            ListenEndPoint = new IPEndPoint(IPAddress.Loopback, port),
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

        IsRunning = true;

        _listenerTask = Task.Run(() => ListenerLoop(_cts.Token));
    }

    public async Task StopServer()
    {
        if (!IsRunning)
            return;

        IsRunning = false;

        _cts?.Cancel();

        if (_listener != null)
            await _listener.DisposeAsync();

        if (_listenerTask != null)
        {
            try
            {
                await _listenerTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        _cts?.Dispose();
        _cts = null;
        _listener = null;
        _listenerTask = null;
    }

    private async Task ListenerLoop(CancellationToken token)
    {
        if (_listener == null)
            return;

        while (!token.IsCancellationRequested)
        {
            QuicConnection connection = await _listener.AcceptConnectionAsync(token);

            _ = Task.Run(() => HandleConnection(connection, token), token);
        }
    }

    private async Task HandleConnection(QuicConnection connection, CancellationToken token)
    {
        await using (connection)
        {
            // TODO: обработка клиента
            await Task.CompletedTask;
        }
    }
}