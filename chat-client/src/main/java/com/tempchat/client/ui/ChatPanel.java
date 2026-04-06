package com.tempchat.client.ui;

import com.tempchat.client.service.ApiClient;
import com.tempchat.client.service.StompClient;

import javax.swing.*;
import javax.swing.border.EmptyBorder;
import java.awt.*;
import java.awt.event.WindowAdapter;
import java.awt.event.WindowEvent;
import java.time.Instant;
import java.time.ZoneId;
import java.time.format.DateTimeFormatter;
import java.util.List;

public class ChatPanel extends JPanel {

    private static final DateTimeFormatter TIME_FMT =
            DateTimeFormatter.ofPattern("HH:mm").withZone(ZoneId.systemDefault());

    private final String serverUrl;
    private final String roomCode;
    private final String username;
    private final Runnable onLeave;

    private final JTextArea chatArea;
    private final JTextField inputField;
    private final JButton sendBtn;
    private final JButton leaveBtn;
    private final JLabel infoLabel;

    private StompClient stomp;

    public ChatPanel(String serverUrl, String roomCode, String username, String roomName, Runnable onLeave) {
        this.serverUrl = serverUrl;
        this.roomCode = roomCode;
        this.username = username;
        this.onLeave = onLeave;

        setLayout(new BorderLayout(6, 6));
        setBorder(new EmptyBorder(10, 12, 10, 12));

        // Header
        infoLabel = new JLabel("Room: " + roomName + "  |  Code: " + roomCode + "  |  You: " + username);
        infoLabel.setFont(new Font("SansSerif", Font.PLAIN, 12));
        infoLabel.setForeground(Color.DARK_GRAY);
        add(infoLabel, BorderLayout.NORTH);

        // Chat area
        chatArea = new JTextArea();
        chatArea.setEditable(false);
        chatArea.setLineWrap(true);
        chatArea.setWrapStyleWord(true);
        chatArea.setFont(new Font("Monospaced", Font.PLAIN, 13));
        JScrollPane scroll = new JScrollPane(chatArea);
        scroll.setPreferredSize(new Dimension(460, 460));
        add(scroll, BorderLayout.CENTER);

        // Input row
        JPanel inputRow = new JPanel(new BorderLayout(6, 0));
        inputField = new JTextField();
        sendBtn = new JButton("Send");
        leaveBtn = new JButton("Leave");
        inputRow.add(inputField, BorderLayout.CENTER);
        JPanel btnPanel = new JPanel(new GridLayout(1, 2, 4, 0));
        btnPanel.add(sendBtn);
        btnPanel.add(leaveBtn);
        inputRow.add(btnPanel, BorderLayout.EAST);
        add(inputRow, BorderLayout.SOUTH);

        sendBtn.addActionListener(e -> sendMessage());
        leaveBtn.addActionListener(e -> leave());
        inputField.addActionListener(e -> sendMessage());

        loadHistoryAndConnect();
    }

    private void loadHistoryAndConnect() {
        new SwingWorker<List<ApiClient.ChatMessage>, Void>() {
            @Override
            protected List<ApiClient.ChatMessage> doInBackground() throws Exception {
                return new ApiClient(serverUrl).getMessages(roomCode);
            }

            @Override
            protected void done() {
                try {
                    for (ApiClient.ChatMessage msg : get()) {
                        String time = TIME_FMT.format(Instant.parse(msg.sentAt()));
                        appendLine("[" + time + "] [" + msg.sender() + "] " + msg.content());
                    }
                } catch (Exception ignored) {}
                connectWebSocket();
            }
        }.execute();
    }

    private void connectWebSocket() {
        String wsUrl = serverUrl
                .replace("https://", "wss://")
                .replace("http://", "ws://")
                + "/ws/websocket";
        try {
            stomp = new StompClient(wsUrl, roomCode, username,
                    msg -> SwingUtilities.invokeLater(() -> appendLine(msg)),
                    event -> SwingUtilities.invokeLater(() -> appendLine("* " + event)));
            stomp.connect();
        } catch (Exception e) {
            appendLine("** Could not connect to WebSocket: " + e.getMessage());
        }
    }

    private void sendMessage() {
        String text = inputField.getText().trim();
        if (text.isEmpty() || stomp == null) return;
        inputField.setText("");
        try {
            stomp.sendChatMessage(text);
        } catch (Exception e) {
            appendLine("** Send failed: " + e.getMessage());
        }
    }

    private void leave() {
        if (stomp != null) {
            stomp.leaveRoom();
            try { stomp.closeBlocking(); } catch (Exception ignored) {}
            stomp = null;
        }
        onLeave.run();
    }

    private void appendLine(String line) {
        chatArea.append(line + "\n");
        chatArea.setCaretPosition(chatArea.getDocument().getLength());
    }
}
