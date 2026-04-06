package com.tempchat.client.ui;

import com.tempchat.client.service.ApiClient;

import javax.swing.*;
import javax.swing.border.EmptyBorder;
import java.awt.*;
import java.util.function.Consumer;

public class LoginPanel extends JPanel {

    public interface JoinCallback {
        void onJoin(String serverUrl, String roomCode, String username, String roomName);
    }

    private final JTextField serverField;
    private final JTextField usernameField;
    private final JTextField roomCodeField;
    private final JTextField newRoomNameField;
    private final JButton joinBtn;
    private final JButton createBtn;
    private final JLabel statusLabel;
    private final JoinCallback callback;

    public LoginPanel(String defaultServer, JoinCallback callback) {
        this.callback = callback;
        setLayout(new BorderLayout(10, 10));
        setBorder(new EmptyBorder(30, 40, 30, 40));

        // Title
        JLabel title = new JLabel("TempChat", SwingConstants.CENTER);
        title.setFont(new Font("SansSerif", Font.BOLD, 28));
        add(title, BorderLayout.NORTH);

        // Form
        JPanel form = new JPanel(new GridBagLayout());
        GridBagConstraints gbc = new GridBagConstraints();
        gbc.fill = GridBagConstraints.HORIZONTAL;
        gbc.insets = new Insets(6, 4, 6, 4);

        serverField = new JTextField(defaultServer);
        usernameField = new JTextField();
        roomCodeField = new JTextField();
        newRoomNameField = new JTextField();

        addRow(form, gbc, 0, "Server URL:", serverField);
        addRow(form, gbc, 1, "Username:", usernameField);

        JSeparator sep = new JSeparator();
        gbc.gridx = 0; gbc.gridy = 2; gbc.gridwidth = 2;
        gbc.insets = new Insets(14, 4, 14, 4);
        form.add(sep, gbc);
        gbc.gridwidth = 1;
        gbc.insets = new Insets(6, 4, 6, 4);

        addRow(form, gbc, 3, "Room code:", roomCodeField);
        addRow(form, gbc, 4, "New room name:", newRoomNameField);

        add(form, BorderLayout.CENTER);

        // Buttons + status
        JPanel south = new JPanel(new GridLayout(3, 1, 6, 6));
        joinBtn = new JButton("Join Room");
        createBtn = new JButton("Create Room");
        statusLabel = new JLabel(" ", SwingConstants.CENTER);
        statusLabel.setForeground(Color.RED);

        south.add(joinBtn);
        south.add(createBtn);
        south.add(statusLabel);
        add(south, BorderLayout.SOUTH);

        joinBtn.addActionListener(e -> handleJoin());
        createBtn.addActionListener(e -> handleCreate());
    }

    private void addRow(JPanel panel, GridBagConstraints gbc, int row, String label, JComponent field) {
        gbc.gridx = 0; gbc.gridy = row; gbc.weightx = 0;
        panel.add(new JLabel(label), gbc);
        gbc.gridx = 1; gbc.weightx = 1;
        panel.add(field, gbc);
    }

    private void handleJoin() {
        String server = serverField.getText().trim();
        String username = usernameField.getText().trim();
        String code = roomCodeField.getText().trim().toUpperCase();

        if (username.isEmpty() || code.isEmpty()) {
            setStatus("Username and room code are required.");
            return;
        }

        setStatus("Connecting...");
        setEnabled(false);

        new SwingWorker<ApiClient.RoomInfo, Void>() {
            @Override
            protected ApiClient.RoomInfo doInBackground() throws Exception {
                return new ApiClient(server).getRoom(code);
            }

            @Override
            protected void done() {
                setEnabled(true);
                try {
                    ApiClient.RoomInfo room = get();
                    setStatus(" ");
                    callback.onJoin(server, room.code(), username, room.name());
                } catch (Exception ex) {
                    setStatus("Error: " + rootMessage(ex));
                }
            }
        }.execute();
    }

    private void handleCreate() {
        String server = serverField.getText().trim();
        String username = usernameField.getText().trim();
        String name = newRoomNameField.getText().trim();

        if (username.isEmpty() || name.isEmpty()) {
            setStatus("Username and room name are required.");
            return;
        }

        setStatus("Creating room...");
        setEnabled(false);

        new SwingWorker<ApiClient.RoomInfo, Void>() {
            @Override
            protected ApiClient.RoomInfo doInBackground() throws Exception {
                return new ApiClient(server).createRoom(name);
            }

            @Override
            protected void done() {
                setEnabled(true);
                try {
                    ApiClient.RoomInfo room = get();
                    setStatus(" ");
                    callback.onJoin(server, room.code(), username, room.name());
                } catch (Exception ex) {
                    setStatus("Error: " + rootMessage(ex));
                }
            }
        }.execute();
    }

    private void setStatus(String msg) {
        statusLabel.setText(msg);
    }

    @Override
    public void setEnabled(boolean enabled) {
        super.setEnabled(enabled);
        joinBtn.setEnabled(enabled);
        createBtn.setEnabled(enabled);
        serverField.setEnabled(enabled);
        usernameField.setEnabled(enabled);
        roomCodeField.setEnabled(enabled);
        newRoomNameField.setEnabled(enabled);
    }

    private String rootMessage(Exception ex) {
        Throwable cause = ex;
        while (cause.getCause() != null) cause = cause.getCause();
        return cause.getMessage() != null ? cause.getMessage() : ex.getClass().getSimpleName();
    }
}
