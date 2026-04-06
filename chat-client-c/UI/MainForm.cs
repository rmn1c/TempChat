namespace TempChat.UI;

public sealed class MainForm : Form
{
    private const string DefaultHost = "localhost";
    private const string DefaultPort = "8080";

    private readonly LoginPanel _loginPanel;
    private ChatPanel? _chatPanel;

    public MainForm()
    {
        Text            = "TempChat";
        MinimumSize     = new Size(500, 680);
        StartPosition   = FormStartPosition.CenterScreen;

        _loginPanel      = new LoginPanel(DefaultHost, DefaultPort, OnJoinRoom);
        _loginPanel.Dock = DockStyle.Fill;
        Controls.Add(_loginPanel);
    }

    private void OnJoinRoom(string serverUrl, string roomCode, string username,
                            string roomName, string roomPassword)
    {
        _chatPanel?.Dispose();
        _chatPanel = new ChatPanel(serverUrl, roomCode, username, roomName, roomPassword, OnLeaveRoom)
        {
            Dock = DockStyle.Fill
        };

        Controls.Clear();
        Controls.Add(_chatPanel);
        Text = $"TempChat — {roomName} [{roomCode}]";
    }

    private void OnLeaveRoom()
    {
        _chatPanel?.Dispose();
        _chatPanel = null;
        Controls.Clear();
        Controls.Add(_loginPanel);
        Text = "TempChat";
    }
}
