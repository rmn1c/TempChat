# TempChat

A temporary chat application with a Java Spring Boot server and two C# desktop clients: a Windows-only WinForms client and a cross-platform Avalonia client that runs on Windows, Linux, and macOS. Chat rooms are ephemeral — messages are automatically deleted after 24 hours and rooms are cleaned up when empty.

## Architecture

```
tempchat/
├── chat-server/              # Spring Boot + PostgreSQL backend
├── chat-client-c/            # C# (.NET 8) WinForms client — Windows only
└── cross-platform-client/    # C# (.NET 8) Avalonia client — Windows, Linux, macOS
```

## Features

- Create or join chat rooms with a short room code
- Real-time messaging via WebSocket (STOMP)
- **End-to-end encryption** — messages are encrypted client-side with AES-256-GCM; the server only ever stores and relays ciphertext
- Temporary messages — auto-deleted after 24 hours
- Rooms auto-close when all users disconnect
- No account required — just enter a server IP, port, username, and room password

---

## Prerequisites

### Server

| Tool | Version | Notes |
|------|---------|-------|
| Java JDK | 21+ | |
| Maven | 3.9+ | |
| Docker | any | For the bundled PostgreSQL |

### Client — Windows only (`chat-client-c`)

| Tool | Version | Notes |
|------|---------|-------|
| .NET SDK | 8.0+ | [Download](https://dotnet.microsoft.com/download) |
| Windows | any | WinForms targets `net8.0-windows` — Windows only |

### Client — Cross-platform (`cross-platform-client`)

| Tool | Version | Notes |
|------|---------|-------|
| .NET SDK | 8.0+ | [Download](https://dotnet.microsoft.com/download) |
| OS | any | Windows, Linux, macOS (x64 and arm64) |

---

## Running the Server

### 1. Start the database

```bash
docker-compose up -d
```

### 2. Start the server

```bash
cd chat-server
mvn spring-boot:run
```

The server listens on `http://localhost:8080` by default.

---

## Building the Client

### Windows-only WinForms client (`chat-client-c`)

#### Run directly (dev mode)

```bash
cd chat-client-c
dotnet run
```

#### Build a release executable

```bash
cd chat-client-c
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Output: `chat-client-c/bin/Release/net8.0-windows/win-x64/publish/TempChat.exe`

---

### Cross-platform Avalonia client (`cross-platform-client`)

Built with [Avalonia UI](https://avaloniaui.net/) — runs natively on Windows, Linux, and macOS.

#### Run directly (dev mode)

```bash
cd cross-platform-client
dotnet run
```

#### Build a self-contained single-file executable

Replace `<RID>` with the target platform runtime identifier:

| Platform | RID |
|----------|-----|
| Windows x64 | `win-x64` |
| Windows arm64 | `win-arm64` |
| Linux x64 | `linux-x64` |
| Linux arm64 | `linux-arm64` |
| macOS x64 (Intel) | `osx-x64` |
| macOS arm64 (Apple Silicon) | `osx-arm64` |

```bash
cd cross-platform-client
dotnet publish -c Release -r <RID> --self-contained true -p:PublishSingleFile=true
```

Output: `cross-platform-client/bin/Release/net8.0/<RID>/publish/TempChat[.exe]`

The published binary bundles the .NET runtime — no .NET install needed on the target machine.

---

## Using the Client

When you launch TempChat you will see a login screen with the following fields:

| Field | Description |
|-------|-------------|
| **Server IP** | IP address or hostname of the TempChat server (default: `localhost`) |
| **Port** | Port the server is listening on (default: `8080`) |
| **Username** | The name other users will see in the chat |
| **Room Password** | Shared secret used to derive the encryption key — **all members must use the same password**. Leave blank to disable encryption. |
| **Room code** | 6-character code of an existing room — click **Join Room** |
| **New room name** | Name for a brand-new room — click **Create Room** |

### End-to-end encryption

- The room password never leaves your device.
- Key derivation: PBKDF2-HMAC-SHA256 (200 000 iterations), salted with the room code, producing a 256-bit AES key.
- Each message is encrypted with AES-256-GCM (unique 96-bit random IV per message). The server stores `Base64(IV ‖ ciphertext ‖ tag)`.
- If a message cannot be decrypted (wrong password or legacy plaintext), it is displayed as `[could not decrypt]`.

After joining, share the room code shown in the window title with anyone who should join the same room.

---

## Server Configuration

Edit `chat-server/src/main/resources/application.properties`:

```properties
spring.datasource.url=jdbc:postgresql://localhost:5432/tempchat
spring.datasource.username=tempchat
spring.datasource.password=tempchat_pass
tempchat.message.ttl-hours=24
tempchat.room.cleanup-interval-minutes=30
```

---

## Server REST API

| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/api/rooms` | Create a new room |
| `GET`  | `/api/rooms/{code}` | Get room info |
| `GET`  | `/api/rooms/{code}/messages` | Get recent messages |

## WebSocket (STOMP)

Connect to `ws://<host>:<port>/ws`.

| Destination | Direction | Description |
|-------------|-----------|-------------|
| `/app/chat/{roomCode}` | send | Send a message |
| `/topic/chat/{roomCode}` | subscribe | Receive messages |
| `/app/join/{roomCode}` | send | Join a room |
| `/topic/users/{roomCode}` | subscribe | User presence updates |
