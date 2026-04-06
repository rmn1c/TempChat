package com.tempchat.client.service;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import okhttp3.*;

import java.io.IOException;
import java.util.ArrayList;
import java.util.List;

public class ApiClient {

    private static final MediaType JSON = MediaType.get("application/json");

    private final String baseUrl;
    private final OkHttpClient http;
    private final ObjectMapper mapper;

    public ApiClient(String baseUrl) {
        this.baseUrl = baseUrl.endsWith("/") ? baseUrl.substring(0, baseUrl.length() - 1) : baseUrl;
        this.http = new OkHttpClient();
        this.mapper = new ObjectMapper().findAndRegisterModules();
    }

    public record RoomInfo(String code, String name, String expiresAt) {}
    public record ChatMessage(String sender, String content, String sentAt) {}

    public RoomInfo createRoom(String name) throws IOException {
        String body = mapper.writeValueAsString(java.util.Map.of("name", name));
        Request req = new Request.Builder()
                .url(baseUrl + "/api/rooms")
                .post(RequestBody.create(body, JSON))
                .build();
        try (Response res = http.newCall(req).execute()) {
            assertSuccess(res);
            JsonNode node = mapper.readTree(res.body().string());
            return new RoomInfo(
                    node.get("code").asText(),
                    node.get("name").asText(),
                    node.get("expiresAt").asText());
        }
    }

    public RoomInfo getRoom(String code) throws IOException {
        Request req = new Request.Builder()
                .url(baseUrl + "/api/rooms/" + code)
                .get()
                .build();
        try (Response res = http.newCall(req).execute()) {
            assertSuccess(res);
            JsonNode node = mapper.readTree(res.body().string());
            return new RoomInfo(
                    node.get("code").asText(),
                    node.get("name").asText(),
                    node.get("expiresAt").asText());
        }
    }

    public List<ChatMessage> getMessages(String roomCode) throws IOException {
        Request req = new Request.Builder()
                .url(baseUrl + "/api/rooms/" + roomCode + "/messages")
                .get()
                .build();
        try (Response res = http.newCall(req).execute()) {
            assertSuccess(res);
            List<ChatMessage> messages = new ArrayList<>();
            for (JsonNode node : mapper.readTree(res.body().string())) {
                messages.add(new ChatMessage(
                        node.get("sender").asText(),
                        node.get("content").asText(),
                        node.get("sentAt").asText()));
            }
            return messages;
        }
    }

    private void assertSuccess(Response res) throws IOException {
        if (!res.isSuccessful()) {
            throw new IOException("Server error " + res.code() + ": " + res.message());
        }
    }
}
