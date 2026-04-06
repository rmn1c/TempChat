package com.tempchat.client.service;

import com.fasterxml.jackson.databind.ObjectMapper;
import org.java_websocket.client.WebSocketClient;
import org.java_websocket.handshake.ServerHandshake;

import java.net.URI;
import java.util.Map;
import java.util.concurrent.atomic.AtomicInteger;
import java.util.function.BiConsumer;
import java.util.function.Consumer;

/**
 * Minimal STOMP-over-WebSocket client.
 * Connects to ws://host/ws/websocket (SockJS raw WebSocket endpoint).
 *
 * onMessage receives (sender, content) so the caller can decrypt content
 * before displaying it.
 */
public class StompClient extends WebSocketClient {

    private static final String STOMP_CONNECT =
            "CONNECT\naccept-version:1.2\nheart-beat:0,0\n\n\0";

    private final AtomicInteger subId = new AtomicInteger(0);
    private final ObjectMapper mapper = new ObjectMapper().findAndRegisterModules();

    private final BiConsumer<String, String> onMessage;
    private final Consumer<String> onUserEvent;
    private final String roomCode;
    private final String username;

    public StompClient(String wsUrl, String roomCode, String username,
                       BiConsumer<String, String> onMessage,
                       Consumer<String> onUserEvent) throws Exception {
        super(new URI(wsUrl));
        this.roomCode = roomCode;
        this.username = username;
        this.onMessage = onMessage;
        this.onUserEvent = onUserEvent;
    }

    @Override
    public void onOpen(ServerHandshake hs) {
        send(STOMP_CONNECT);
    }

    @Override
    public void onMessage(String frame) {
        if (frame.startsWith("CONNECTED")) {
            subscribeToTopic("/topic/chat/" + roomCode);
            subscribeToTopic("/topic/users/" + roomCode);
            sendStompMessage("/app/join/" + roomCode, "\"" + username + "\"");
            return;
        }
        if (frame.startsWith("MESSAGE")) {
            String body = extractBody(frame);
            String dest = extractHeader(frame, "destination");
            if (dest != null && dest.startsWith("/topic/users/")) {
                onUserEvent.accept(body.replace("\"", ""));
            } else {
                try {
                    var node = mapper.readTree(body);
                    String sender  = node.has("sender")  ? node.get("sender").asText()  : "?";
                    String content = node.has("content") ? node.get("content").asText() : body;
                    onMessage.accept(sender, content);
                } catch (Exception e) {
                    onMessage.accept("?", body);
                }
            }
        }
    }

    public void sendChatMessage(String content) {
        try {
            String json = mapper.writeValueAsString(Map.of(
                    "sender",   username,
                    "content",  content,
                    "roomCode", roomCode));
            sendStompMessage("/app/chat/" + roomCode, json);
        } catch (Exception e) {
            throw new RuntimeException(e);
        }
    }

    public void leaveRoom() {
        try {
            sendStompMessage("/app/leave/" + roomCode, "\"" + username + "\"");
        } catch (Exception ignored) {}
    }

    @Override public void onClose(int code, String reason, boolean remote) {}
    @Override public void onError(Exception ex) {}

    // ---- STOMP helpers ----

    private void subscribeToTopic(String topic) {
        String frame = "SUBSCRIBE\nid:sub-" + subId.getAndIncrement() +
                "\ndestination:" + topic + "\n\n\0";
        send(frame);
    }

    private void sendStompMessage(String destination, String body) {
        String frame = "SEND\ndestination:" + destination +
                "\ncontent-type:application/json\n\n" + body + "\0";
        send(frame);
    }

    private String extractBody(String frame) {
        int idx = frame.indexOf("\n\n");
        if (idx < 0) return frame;
        String raw = frame.substring(idx + 2);
        return raw.endsWith("\0") ? raw.substring(0, raw.length() - 1) : raw;
    }

    private String extractHeader(String frame, String headerName) {
        for (String line : frame.split("\n")) {
            if (line.startsWith(headerName + ":")) {
                return line.substring(headerName.length() + 1).trim();
            }
        }
        return null;
    }
}
