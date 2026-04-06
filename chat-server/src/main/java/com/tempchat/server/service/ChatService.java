package com.tempchat.server.service;

import com.tempchat.server.dto.MessageDto;
import com.tempchat.server.dto.RoomDto;
import com.tempchat.server.model.ChatRoom;
import com.tempchat.server.model.Message;
import com.tempchat.server.repository.ChatRoomRepository;
import com.tempchat.server.repository.MessageRepository;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.time.Instant;
import java.time.temporal.ChronoUnit;
import java.util.List;
import java.util.Random;

@Service
public class ChatService {

    private static final String CODE_CHARS = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private static final int CODE_LENGTH = 6;

    private final ChatRoomRepository roomRepo;
    private final MessageRepository messageRepo;

    @Value("${tempchat.message.ttl-hours:24}")
    private int messageTtlHours;

    public ChatService(ChatRoomRepository roomRepo, MessageRepository messageRepo) {
        this.roomRepo = roomRepo;
        this.messageRepo = messageRepo;
    }

    @Transactional
    public RoomDto createRoom(String name) {
        String code = generateUniqueCode();
        Instant now = Instant.now();
        Instant expires = now.plus(messageTtlHours, ChronoUnit.HOURS);
        ChatRoom room = new ChatRoom(code, name, now, expires);
        room = roomRepo.save(room);
        return toDto(room);
    }

    @Transactional(readOnly = true)
    public RoomDto getRoom(String code) {
        ChatRoom room = findActiveRoom(code);
        return toDto(room);
    }

    @Transactional(readOnly = true)
    public List<MessageDto> getMessages(String roomCode) {
        return messageRepo.findActiveByRoomCode(roomCode, Instant.now())
                .stream()
                .map(this::toDto)
                .toList();
    }

    @Transactional
    public MessageDto saveAndBroadcast(String roomCode, String sender, String content) {
        ChatRoom room = findActiveRoom(roomCode);
        Instant now = Instant.now();
        Message msg = new Message(room, sender, content, now, now.plus(messageTtlHours, ChronoUnit.HOURS));
        msg = messageRepo.save(msg);
        return toDto(msg);
    }

    private ChatRoom findActiveRoom(String code) {
        return roomRepo.findByCodeAndActiveTrue(code)
                .orElseThrow(() -> new IllegalArgumentException("Room not found or expired: " + code));
    }

    private String generateUniqueCode() {
        Random rng = new Random();
        String code;
        do {
            StringBuilder sb = new StringBuilder(CODE_LENGTH);
            for (int i = 0; i < CODE_LENGTH; i++) {
                sb.append(CODE_CHARS.charAt(rng.nextInt(CODE_CHARS.length())));
            }
            code = sb.toString();
        } while (roomRepo.findByCodeAndActiveTrue(code).isPresent());
        return code;
    }

    private RoomDto toDto(ChatRoom r) {
        return new RoomDto(r.getCode(), r.getName(), r.getCreatedAt(), r.getExpiresAt());
    }

    private MessageDto toDto(Message m) {
        return new MessageDto(m.getSender(), m.getContent(), m.getSentAt(), m.getRoom().getCode());
    }
}
