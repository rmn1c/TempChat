package com.tempchat.client.ui;

import com.tempchat.client.service.ApiClient;

import javax.swing.*;
import javax.swing.border.EmptyBorder;
import java.awt.*;

public class LoginPanel extends JPanel {

    public interface JoinCallback {
        void onJoin(String serverUrl, String roomCode, String username,
                    String roomName, String roomPassword);
    }

    private final JTextField     hostField;
    private final JTextField     portField;
    private final JTextField     usernameField;
    private final JPasswordField roomPasswordField;
    private final JTextField     roomCodeField;
    private final JTextField     newRoomNameField;
    private final JButton        joinBtn;
    private final JButton        createBtn;
    private final JLabel         statusLabel;
    private final JoinCallback   callback;

    public LoginPanel(String defaultHost, String defaultPort, JoinCallback callback) {
        this.callback = callback;
        setLayout(new BorderLayout(10, 10));
        setBorder(new EmptyBorder(30, 40, 30, 40));

        JLabel title = new JLabel("TempChat", SwingConstants.CENTER);
        title.setFont(new Font("SansSerif", Font.BOLD, 28));
        add(title, BorderLayout.NORTH);

        JPanel form = new JPanel(new GridBagLayout());
        GridBagConstraints gbc = new GridBagConstraints();
        gbc.fill = GridBagConstraints.HORIZONTAL;
        gbc.insets = new Insets(6, 4, 6, 4);

        hostField         = new JTextField(defaultHost);
        portField         = new JTextField(defaultPort);
        usernameField     = new JTextField();
        roomPasswordField = new JPasswordField();
        roomCodeField     = new JTextField();
        newRoomNameField  = new JTextField();

        addRow(form, gbc, 0, "Server IP:",      hostField);
        addRow(form, gbc, 1, "Port:",            portField);
        addRow(form, gbc, 2, "Username:",        usernameField);
        addRow(form, gbc, 3, "Room Password:",   roomPasswordField);

        JSeparator sep = new JSeparator();
        gbc.gridx = 0; gbc.gridy = 4; gbc.gridwidth = 2;
        gbc.insets = new Insets(14, 4, 14, 4);
        form.add(sep, gbc);
        gbc.gridwidth = 1;
        gbc.insets = new Insets(6, 4, 6, 4);

        addRow(form, gbc, 5, "Room code:",      roomCodeField);
        addRow(form, gbc, 6, "New room name:",  newRoomNameField);

        JLabel hint = new JLabel(
                "<html><i>Room Password is used for end-to-end encryption.<br>" +
                "All members of a room must use the same password.</i></html>",
                SwingConstants.CENTER);
        hint.setForeground(Color.GRAY);
        hint.setFont(hint.getFont().deriveFont(11f));
        gbc.gridx = 0; gbc.gridy = 7; gbc.gridwidth = 2;
        gbc.insets = new Insets(6, 4, 2, 4);
        form.add(hint, gbc);

        add(form, BorderLayout.CENTER);

        JPanel south = new JPanel(new GridLayout(3, 1, 6, 6));
        joinBtn     = new JButton("Join Room");
        createBtn   = new JButton("Create Room");
        statusLabel = new JLabel(" ", SwingConstants.CENTER);
        statusLabel.setForeground(Color.RED);
        south.add(joinBtn);
        south.add(createBtn);
        south.add(statusLabel);
        add(south, BorderLayout.SOUTH);

        joinBtn.addActionListener(e -> handleJoin());
        createBtn.addActionListener(e -> handleCreate());
    }

    private void addRow(JPanel panel, GridBagConstraints gbc, int row,
                        String label, JComponent field) {
        gbc.gridx = 0; gbc.gridy = row; gbc.weightx = 0;
        panel.add(new JLabel(label), gbc);
        gbc.gridx = 1; gbc.weightx = 1;
        panel.add(field, gbc);
    }

    private String buildServerUrl() {
        return "http://" + hostField.getText().trim() + ":" + portField.getText().trim();
    }

    private String roomPassword() {
        return new String(roomPasswordField.getPassword());
    }

    private void handleJoin() {
        String username = usernameField.getText().trim();
        String code     = roomCodeField.getText().trim().toUpperCase();

        if (hostField.getText().trim().isEmpty() || portField.getText().trim().isEmpty()) {
            setStatus("Server IP and port are required.");
            return;
        }
        if (username.isEmpty() || code.isEmpty()) {
            setStatus("Username and room code are required.");
            return;
        }

        String server   = buildServerUrl();
        String password = roomPassword();
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
                    callback.onJoin(server, room.code(), username, room.name(), password);
                } catch (Exception ex) {
                    setStatus("Error: " + rootMessage(ex));
                }
            }
        }.execute();
    }

    private void handleCreate() {
        String username = usernameField.getText().trim();
        String name     = newRoomNameField.getText().trim();

        if (hostField.getText().trim().isEmpty() || portField.getText().trim().isEmpty()) {
            setStatus("Server IP and port are required.");
            return;
        }
        if (username.isEmpty() || name.isEmpty()) {
            setStatus("Username and room name are required.");
            return;
        }

        String server   = buildServerUrl();
        String password = roomPassword();
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
                    callback.onJoin(server, room.code(), username, room.name(), password);
                } catch (Exception ex) {
                    setStatus("Error: " + rootMessage(ex));
                }
            }
        }.execute();
    }

    private void setStatus(String msg) { statusLabel.setText(msg); }

    @Override
    public void setEnabled(boolean enabled) {
        super.setEnabled(enabled);
        joinBtn.setEnabled(enabled);
        createBtn.setEnabled(enabled);
        hostField.setEnabled(enabled);
        portField.setEnabled(enabled);
        usernameField.setEnabled(enabled);
        roomPasswordField.setEnabled(enabled);
        roomCodeField.setEnabled(enabled);
        newRoomNameField.setEnabled(enabled);
    }

    private String rootMessage(Exception ex) {
        Throwable cause = ex;
        while (cause.getCause() != null) cause = cause.getCause();
        return cause.getMessage() != null ? cause.getMessage() : ex.getClass().getSimpleName();
    }
}
