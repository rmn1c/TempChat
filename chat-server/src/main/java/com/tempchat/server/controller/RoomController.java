package com.tempchat.server.controller;

import com.tempchat.server.dto.CreateRoomRequest;
import com.tempchat.server.dto.MessageDto;
import com.tempchat.server.dto.RoomDto;
import com.tempchat.server.service.ChatService;
import jakarta.validation.Valid;
import org.springframework.http.HttpStatus;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.*;
import org.springframework.web.server.ResponseStatusException;

import java.util.List;

@RestController
@RequestMapping("/api/rooms")
@CrossOrigin(origins = "*")
public class RoomController {

    private final ChatService chatService;

    public RoomController(ChatService chatService) {
        this.chatService = chatService;
    }

    @PostMapping
    public ResponseEntity<RoomDto> createRoom(@Valid @RequestBody CreateRoomRequest req) {
        RoomDto room = chatService.createRoom(req.getName());
        return ResponseEntity.status(HttpStatus.CREATED).body(room);
    }

    @GetMapping("/{code}")
    public RoomDto getRoom(@PathVariable String code) {
        try {
            return chatService.getRoom(code.toUpperCase());
        } catch (IllegalArgumentException e) {
            throw new ResponseStatusException(HttpStatus.NOT_FOUND, e.getMessage());
        }
    }

    @GetMapping("/{code}/messages")
    public List<MessageDto> getMessages(@PathVariable String code) {
        try {
            return chatService.getMessages(code.toUpperCase());
        } catch (IllegalArgumentException e) {
            throw new ResponseStatusException(HttpStatus.NOT_FOUND, e.getMessage());
        }
    }
}
