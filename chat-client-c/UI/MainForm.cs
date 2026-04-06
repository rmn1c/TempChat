using System.Runtime.InteropServices;

namespace TempChat.UI;

public sealed class MainForm : Form
{
    private const string DefaultHost = "localhost";
    private const string DefaultPort = "8080";

    // Windows API: enable dark title bar (Windows 10 1809+ / Windows 11)
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    private readonly LoginPanel _loginPanel;
    private ChatPanel? _chatPanel;

    public MainForm()
    {
        Text          = "TempChat";
        MinimumSize   = new Size(500, 660);
        Size          = new Size(520, 720);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor     = Theme.Background;

        _loginPanel      = new LoginPanel(DefaultHost, DefaultPort, OnJoin);
        _loginPanel.Dock = DockStyle.Fill;
        Controls.Add(_loginPanel);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        // Dark title bar
        int dark = 1;
        DwmSetWindowAttribute(Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
    }

    private void OnJoin(string serverUrl, string roomCode, string username,
                        string roomName, string roomPassword)
    {
        _chatPanel?.Dispose();
        _chatPanel = new ChatPanel(serverUrl, roomCode, username, roomName, roomPassword, OnLeave)
        {
            Dock = DockStyle.Fill
        };
        Controls.Clear();
        Controls.Add(_chatPanel);
        Text = $"TempChat — {roomName} [{roomCode}]";
    }

    private void OnLeave()
    {
        _chatPanel?.Dispose();
        _chatPanel = null;
        Controls.Clear();
        Controls.Add(_loginPanel);
        Text = "TempChat";
    }
}
