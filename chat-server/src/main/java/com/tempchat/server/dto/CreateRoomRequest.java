package com.tempchat.server.dto;

import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.Size;

public class CreateRoomRequest {

    @NotBlank
    @Size(min = 1, max = 64)
    private String name;

    public String getName() { return name; }
    public void setName(String name) { this.name = name; }
}
