package com.tempchat.server.dto;

import java.time.Instant;

public class MessageDto {

    private String sender;
    private String content;
    private Instant sentAt;
    private String roomCode;

    public MessageDto() {}

    public MessageDto(String sender, String content, Instant sentAt, String roomCode) {
        this.sender = sender;
        this.content = content;
        this.sentAt = sentAt;
        this.roomCode = roomCode;
    }

    public String getSender() { return sender; }
    public String getContent() { return content; }
    public Instant getSentAt() { return sentAt; }
    public String getRoomCode() { return roomCode; }

    public void setSender(String sender) { this.sender = sender; }
    public void setContent(String content) { this.content = content; }
    public void setSentAt(Instant sentAt) { this.sentAt = sentAt; }
    public void setRoomCode(String roomCode) { this.roomCode = roomCode; }
}
