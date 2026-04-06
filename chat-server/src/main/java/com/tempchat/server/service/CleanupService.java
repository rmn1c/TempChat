package com.tempchat.server.service;

import com.tempchat.server.model.ChatRoom;
import com.tempchat.server.repository.ChatRoomRepository;
import com.tempchat.server.repository.MessageRepository;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.scheduling.annotation.Scheduled;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.time.Instant;
import java.util.List;

@Service
public class CleanupService {

    private static final Logger log = LoggerFactory.getLogger(CleanupService.class);

    private final ChatRoomRepository roomRepo;
    private final MessageRepository messageRepo;

    public CleanupService(ChatRoomRepository roomRepo, MessageRepository messageRepo) {
        this.roomRepo = roomRepo;
        this.messageRepo = messageRepo;
    }

    @Scheduled(fixedRateString = "${tempchat.room.cleanup-interval-minutes:30}000")
    @Transactional
    public void cleanUp() {
        Instant now = Instant.now();

        int deletedMessages = messageRepo.deleteExpiredMessages(now);
        if (deletedMessages > 0) {
            log.info("Deleted {} expired message(s)", deletedMessages);
        }

        List<ChatRoom> expiredRooms = roomRepo.findExpiredRooms(now);
        for (ChatRoom room : expiredRooms) {
            messageRepo.deleteByRoomId(room.getId());
            room.setActive(false);
            roomRepo.save(room);
        }
        if (!expiredRooms.isEmpty()) {
            log.info("Closed {} expired room(s)", expiredRooms.size());
        }
    }
}
