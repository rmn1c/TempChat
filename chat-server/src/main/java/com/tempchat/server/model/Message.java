package com.tempchat.server.model;

import jakarta.persistence.*;
import java.time.Instant;

@Entity
@Table(name = "messages")
public class Message {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @ManyToOne(fetch = FetchType.LAZY)
    @JoinColumn(name = "room_id", nullable = false)
    private ChatRoom room;

    @Column(nullable = false, length = 64)
    private String sender;

    @Column(nullable = false, columnDefinition = "TEXT")
    private String content;

    @Column(nullable = false)
    private Instant sentAt;

    @Column(nullable = false)
    private Instant expiresAt;

    public Message() {}

    public Message(ChatRoom room, String sender, String content, Instant sentAt, Instant expiresAt) {
        this.room = room;
        this.sender = sender;
        this.content = content;
        this.sentAt = sentAt;
        this.expiresAt = expiresAt;
    }

    public Long getId() { return id; }
    public ChatRoom getRoom() { return room; }
    public String getSender() { return sender; }
    public String getContent() { return content; }
    public Instant getSentAt() { return sentAt; }
    public Instant getExpiresAt() { return expiresAt; }

    public void setRoom(ChatRoom room) { this.room = room; }
    public void setSender(String sender) { this.sender = sender; }
    public void setContent(String content) { this.content = content; }
    public void setSentAt(Instant sentAt) { this.sentAt = sentAt; }
    public void setExpiresAt(Instant expiresAt) { this.expiresAt = expiresAt; }
}
