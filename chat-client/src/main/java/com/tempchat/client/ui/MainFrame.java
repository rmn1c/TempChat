package com.tempchat.client.ui;

import javax.swing.*;
import java.awt.*;

public class MainFrame extends JFrame {

    private static final String DEFAULT_HOST = "localhost";
    private static final String DEFAULT_PORT = "8080";

    private final CardLayout cards = new CardLayout();
    private final JPanel root = new JPanel(cards);

    private LoginPanel loginPanel;
    private ChatPanel chatPanel;

    public MainFrame() {
        super("TempChat");
        setDefaultCloseOperation(EXIT_ON_CLOSE);
        setMinimumSize(new Dimension(480, 600));
        setLocationRelativeTo(null);

        loginPanel = new LoginPanel(DEFAULT_HOST, DEFAULT_PORT, this::onJoinRoom);
        root.add(loginPanel, "LOGIN");

        setContentPane(root);
        cards.show(root, "LOGIN");
        pack();
    }

    private void onJoinRoom(String serverUrl, String roomCode, String username, String roomName) {
        if (chatPanel != null) {
            root.remove(chatPanel);
        }
        chatPanel = new ChatPanel(serverUrl, roomCode, username, roomName, this::onLeaveRoom);
        root.add(chatPanel, "CHAT");
        cards.show(root, "CHAT");
        setTitle("TempChat — " + roomName + " [" + roomCode + "]");
    }

    private void onLeaveRoom() {
        cards.show(root, "LOGIN");
        setTitle("TempChat");
    }
}
