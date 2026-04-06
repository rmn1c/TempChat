# TempChat

A temporary chat application with a Java Spring Boot server and a Java Swing desktop client. Chat rooms are ephemeral — messages are automatically deleted after 24 hours and rooms are cleaned up when empty.

## Architecture

```
tempchat/
├── chat-server/        # Spring Boot + PostgreSQL backend
├── chat-client/        # Java Swing desktop client
├── build-client.bat    # Windows build script → produces TempChat.exe
└── build-client.sh     # Linux / macOS build script
```

## Features

- Create or join chat rooms with a short room code
- Real-time messaging via WebSocket (STOMP)
- Temporary messages — auto-deleted after 24 hours
- Rooms auto-close when all users disconnect
- No account required — just enter a server IP, port, and username

---

## Prerequisites

| Tool | Version | Notes |
|------|---------|-------|
| Java JDK | 21+ | Must include `jpackage` (standard since JDK 14) |
| Maven | 3.9+ | |
| Docker | any | For the bundled PostgreSQL |

> **Windows only:** Building an installer (`--type exe`) also requires [WiX Toolset 3.x](https://wixtoolset.org/). The default scripts use `--type app-image` which needs no extra tools.

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

## Building the Client as an Executable

Run the appropriate script from the **project root** — it compiles a fat JAR and wraps it with `jpackage` into a self-contained native application (bundles its own JRE, no Java install needed on end-user machines).

### Windows

```bat
build-client.bat
```

Output: `dist\TempChat\TempChat.exe`

### Linux / macOS

```bash
chmod +x build-client.sh
./build-client.sh
```

Output: `dist/TempChat/TempChat`

The `dist/TempChat/` folder is fully self-contained — copy it anywhere and run the executable.

---

## Using the Client

When you launch TempChat you will see a login screen with the following fields:

| Field | Description |
|-------|-------------|
| **Server IP** | IP address or hostname of the TempChat server (default: `localhost`) |
| **Port** | Port the server is listening on (default: `8080`) |
| **Username** | The name other users will see in the chat |
| **Room code** | 6-character code of an existing room — click **Join Room** |
| **New room name** | Name for a brand-new room — click **Create Room** |

After joining, share the room code shown in the window title with anyone who should join the same room.

---

## Running the Client Without Building (dev mode)

```bash
cd chat-client
mvn compile exec:java
```

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
