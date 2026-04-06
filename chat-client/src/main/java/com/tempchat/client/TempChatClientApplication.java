package com.tempchat.client;

import com.tempchat.client.ui.MainFrame;

import javax.swing.*;

public class TempChatClientApplication {

    public static void main(String[] args) {
        SwingUtilities.invokeLater(() -> {
            try {
                UIManager.setLookAndFeel(UIManager.getSystemLookAndFeelClassName());
            } catch (Exception ignored) {}
            new MainFrame().setVisible(true);
        });
    }
}
