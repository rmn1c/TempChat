package com.tempchat.server.dto;

import java.time.Instant;

public class RoomDto {

    private String code;
    private String name;
    private Instant createdAt;
    private Instant expiresAt;

    public RoomDto() {}

    public RoomDto(String code, String name, Instant createdAt, Instant expiresAt) {
        this.code = code;
        this.name = name;
        this.createdAt = createdAt;
        this.expiresAt = expiresAt;
    }

    public String getCode() { return code; }
    public String getName() { return name; }
    public Instant getCreatedAt() { return createdAt; }
    public Instant getExpiresAt() { return expiresAt; }

    public void setCode(String code) { this.code = code; }
    public void setName(String name) { this.name = name; }
    public void setCreatedAt(Instant createdAt) { this.createdAt = createdAt; }
    public void setExpiresAt(Instant expiresAt) { this.expiresAt = expiresAt; }
}
