package com.tempchat.server.controller;

import com.tempchat.server.dto.MessageDto;
import com.tempchat.server.service.ChatService;
import org.springframework.http.HttpStatus;
import org.springframework.messaging.handler.annotation.DestinationVariable;
import org.springframework.messaging.handler.annotation.MessageMapping;
import org.springframework.messaging.handler.annotation.Payload;
import org.springframework.messaging.simp.SimpMessagingTemplate;
import org.springframework.stereotype.Controller;
import org.springframework.web.server.ResponseStatusException;

@Controller
public class ChatController {

    private final ChatService chatService;
    private final SimpMessagingTemplate broker;

    public ChatController(ChatService chatService, SimpMessagingTemplate broker) {
        this.chatService = chatService;
        this.broker = broker;
    }

    /**
     * Client sends to /app/chat/{roomCode}
     * Broadcast to /topic/chat/{roomCode}
     */
    @MessageMapping("/chat/{roomCode}")
    public void handleMessage(
            @DestinationVariable String roomCode,
            @Payload MessageDto incoming) {
        try {
            MessageDto saved = chatService.saveAndBroadcast(
                    roomCode.toUpperCase(),
                    incoming.getSender(),
                    incoming.getContent());
            broker.convertAndSend("/topic/chat/" + roomCode.toUpperCase(), saved);
        } catch (IllegalArgumentException e) {
            throw new ResponseStatusException(HttpStatus.NOT_FOUND, e.getMessage());
        }
    }

    /**
     * Client sends to /app/join/{roomCode} to announce presence.
     * Broadcast username to /topic/users/{roomCode}
     */
    @MessageMapping("/join/{roomCode}")
    public void handleJoin(
            @DestinationVariable String roomCode,
            @Payload String username) {
        broker.convertAndSend("/topic/users/" + roomCode.toUpperCase(),
                username + " joined the room");
    }

    /**
     * Client sends to /app/leave/{roomCode} to announce departure.
     */
    @MessageMapping("/leave/{roomCode}")
    public void handleLeave(
            @DestinationVariable String roomCode,
            @Payload String username) {
        broker.convertAndSend("/topic/users/" + roomCode.toUpperCase(),
                username + " left the room");
    }
}
