package com.tempchat.server.repository;

import com.tempchat.server.model.Message;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Modifying;
import org.springframework.data.jpa.repository.Query;

import java.time.Instant;
import java.util.List;

public interface MessageRepository extends JpaRepository<Message, Long> {

    @Query("SELECT m FROM Message m WHERE m.room.code = :roomCode AND m.expiresAt > :now ORDER BY m.sentAt ASC")
    List<Message> findActiveByRoomCode(String roomCode, Instant now);

    @Modifying
    @Query("DELETE FROM Message m WHERE m.expiresAt < :now")
    int deleteExpiredMessages(Instant now);

    @Modifying
    @Query("DELETE FROM Message m WHERE m.room.id = :roomId")
    void deleteByRoomId(Long roomId);
}
