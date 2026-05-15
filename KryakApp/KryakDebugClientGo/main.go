package main

import (
	"context"
	"crypto/tls"
	"encoding/binary"
	"encoding/json"
	"errors"
	"fmt"
	"io"
	"os"
	"os/signal"
	"strconv"
	"sync"
	"syscall"

	"github.com/quic-go/quic-go"
)

const (
	defaultHost = "127.0.0.1"
	defaultPort = 5555

	channelMain = "main"
	channelPing = "ping"

	typeHello       = "hello"
	typePingRequest = "ping_request"
	typePong        = "pong"
)

type UserPayload struct {
	UserIPAddress    string `json:"UserIPAddress"`
	VictimTag        string `json:"VictimTag"`
	Username         string `json:"Username"`
	Country          string `json:"Country"`
	UserOS           string `json:"UserOS"`
	AdminStatus      bool   `json:"AdminStatus"`
	CameraStatus     bool   `json:"CameraStatus"`
	MicrophoneStatus bool   `json:"MicrophoneStatus"`
	Ping             string `json:"Ping"`
}

type Message struct {
	Channel string       `json:"Channel"`
	Type    string       `json:"Type"`
	User    *UserPayload `json:"User,omitempty"`
	PingID  string       `json:"PingId,omitempty"`
}

func main() {
	port := defaultPort
	if len(os.Args) > 1 {
		parsed, err := strconv.Atoi(os.Args[1])
		if err != nil || parsed < 1 || parsed > 65535 {
			fmt.Println("Usage: go run . [port]")
			os.Exit(1)
		}
		port = parsed
	}

	address := fmt.Sprintf("%s:%d", defaultHost, port)
	tlsConfig := &tls.Config{
		InsecureSkipVerify: true,
		NextProtos:         []string{"kryak"},
		MinVersion:         tls.VersionTLS13,
		ServerName:         "localhost",
	}

	ctx, stop := signal.NotifyContext(context.Background(), os.Interrupt, syscall.SIGTERM)
	defer stop()

	fmt.Printf("Connecting to %s...\n", address)
	conn, err := quic.DialAddr(ctx, address, tlsConfig, nil)
	if err != nil {
		fmt.Printf("Connect failed: %v\n", err)
		os.Exit(1)
	}
	defer conn.CloseWithError(0, "debug client shutdown")

	mainStream, err := conn.OpenUniStreamSync(ctx)
	if err != nil {
		fmt.Printf("Open main channel failed: %v\n", err)
		os.Exit(1)
	}

	user := UserPayload{
		UserIPAddress:    "",
		VictimTag:        "GoDebugClient",
		Username:         os.Getenv("USERNAME"),
		Country:          "Local",
		UserOS:           "Windows",
		AdminStatus:      false,
		CameraStatus:     false,
		MicrophoneStatus: false,
		Ping:             "0",
	}

	if user.Username == "" {
		user.Username = "go-client"
	}

	hello := Message{Channel: channelMain, Type: typeHello, User: &user}
	if err := writeFrame(mainStream, hello); err != nil {
		fmt.Printf("Send hello failed: %v\n", err)
		os.Exit(1)
	}

	fmt.Println("Connected. Waiting for server ping requests. Press Ctrl+C to stop.")

	writer := &streamWriter{stream: mainStream}
	errCh := make(chan error, 1)

	go func() {
		errCh <- readServerStreams(ctx, conn, writer)
	}()

	for {
		select {
		case <-ctx.Done():
			fmt.Println("Disconnecting...")
			return
		case err = <-errCh:
			if err != nil && !errors.Is(err, context.Canceled) {
				fmt.Printf("Stream loop stopped: %v\n", err)
			}
			return
		}
	}
}

func readServerStreams(ctx context.Context, conn *quic.Conn, writer *streamWriter) error {
	for {
		stream, err := conn.AcceptUniStream(ctx)
		if err != nil {
			return err
		}

		if err := handleInboundStream(ctx, stream, writer); err != nil {
			return err
		}
	}
}

func handleInboundStream(ctx context.Context, stream *quic.ReceiveStream, writer *streamWriter) error {
	for {
		msg, err := readFrame(stream)
		if err != nil {
			if errors.Is(err, io.EOF) || errors.Is(err, io.ErrUnexpectedEOF) {
				return nil
			}
			return err
		}

		if msg.Channel == channelPing && msg.Type == typePingRequest && msg.PingID != "" {
			pong := Message{Channel: channelMain, Type: typePong, PingID: msg.PingID}
			if err := writer.Write(pong); err != nil {
				return err
			}
			fmt.Println("Pong sent")
		}

		select {
		case <-ctx.Done():
			return ctx.Err()
		default:
		}
	}
}

type streamWriter struct {
	mu     sync.Mutex
	stream *quic.SendStream
}

func (w *streamWriter) Write(msg Message) error {
	w.mu.Lock()
	defer w.mu.Unlock()
	return writeFrame(w.stream, msg)
}

func writeFrame(stream *quic.SendStream, msg Message) error {
	payload, err := json.Marshal(msg)
	if err != nil {
		return err
	}

	if len(payload) > int(^uint32(0)) {
		return fmt.Errorf("payload too large")
	}

	frame := make([]byte, 4+len(payload))
	binary.BigEndian.PutUint32(frame[:4], uint32(len(payload)))
	copy(frame[4:], payload)

	_, err = (*stream).Write(frame)
	return err
}

func readFrame(stream *quic.ReceiveStream) (Message, error) {
	var sizeBuf [4]byte
	if _, err := io.ReadFull(stream, sizeBuf[:]); err != nil {
		return Message{}, err
	}

	length := binary.BigEndian.Uint32(sizeBuf[:])
	if length == 0 {
		return Message{}, fmt.Errorf("invalid frame length")
	}

	payload := make([]byte, length)
	if _, err := io.ReadFull(stream, payload); err != nil {
		return Message{}, err
	}

	var msg Message
	if err := json.Unmarshal(payload, &msg); err != nil {
		return Message{}, err
	}

	return msg, nil
}
