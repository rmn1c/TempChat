# TempChat

A temporary chat application with a Java Spring Boot server and Java Swing desktop client. Chat rooms are ephemeral — messages are automatically deleted after 24 hours and rooms are cleaned up when empty.

## Architecture

```
tempchat/
├── chat-server/    # Spring Boot + PostgreSQL backend
└── chat-client/    # Java Swing desktop client
```

## Features

- Create or join chat rooms with a short room code
- Real-time messaging via WebSocket (STOMP)
- Temporary messages — auto-deleted after 24 hours
- Rooms auto-close when all users disconnect
- No account required — just pick a username

## Prerequisites

- Java 21+
- Maven 3.9+
- Docker (for PostgreSQL) or a running PostgreSQL 14+ instance

## Quick Start

### 1. Start the database

```bash
docker-compose up -d
```

### 2. Start the server

```bash
cd chat-server
mvn spring-boot:run
```

Server runs on `http://localhost:8080`.

### 3. Start the client

```bash
cd chat-client
mvn compile exec:java
```

## Server REST API

| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/api/rooms` | Create a new room |
| `GET`  | `/api/rooms/{code}` | Get room info |
| `GET`  | `/api/rooms/{code}/messages` | Get recent messages |

## WebSocket

Connect to `ws://localhost:8080/ws` using STOMP.

| Destination | Direction | Description |
|-------------|-----------|-------------|
| `/app/chat/{roomCode}` | send | Send a message |
| `/topic/chat/{roomCode}` | subscribe | Receive messages |
| `/app/join/{roomCode}` | send | Join a room |
| `/topic/users/{roomCode}` | subscribe | User presence updates |

## Configuration

Edit `chat-server/src/main/resources/application.properties` to change the database URL, credentials, or message TTL.

```properties
spring.datasource.url=jdbc:postgresql://localhost:5432/tempchat
spring.datasource.username=tempchat
spring.datasource.password=tempchat_pass
tempchat.message.ttl-hours=24
tempchat.room.cleanup-interval-minutes=30
```
