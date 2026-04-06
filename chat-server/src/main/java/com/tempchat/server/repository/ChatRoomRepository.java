package com.tempchat.server.repository;

import com.tempchat.server.model.ChatRoom;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Query;

import java.time.Instant;
import java.util.List;
import java.util.Optional;

public interface ChatRoomRepository extends JpaRepository<ChatRoom, Long> {

    Optional<ChatRoom> findByCodeAndActiveTrue(String code);

    @Query("SELECT r FROM ChatRoom r WHERE r.active = true AND r.expiresAt < :now")
    List<ChatRoom> findExpiredRooms(Instant now);
}
