using TempChat.Services;

namespace TempChat.UI;

public sealed class ChatPanel : Panel, IDisposable
{
    private static readonly string TimeFmt = "HH:mm";

    private readonly string _serverUrl;
    private readonly string _roomCode;
    private readonly string _username;
    private readonly Action _onLeave;
    private readonly CryptoService? _crypto;

    private readonly RichTextBox _chatArea;
    private readonly TextBox     _inputField;
    private readonly Button      _sendBtn;
    private readonly Button      _leaveBtn;

    private StompClient? _stomp;

    public ChatPanel(string serverUrl, string roomCode, string username,
                     string roomName, string roomPassword, Action onLeave)
    {
        _serverUrl = serverUrl;
        _roomCode  = roomCode;
        _username  = username;
        _onLeave   = onLeave;
        Padding    = new Padding(10);

        if (!string.IsNullOrEmpty(roomPassword))
        {
            try { _crypto = new CryptoService(roomPassword, roomCode); }
            catch { /* shown below */ }
        }

        // ---- Header ----
        string encTag = _crypto != null ? " 🔒" : " ⚠ no password";
        var header = new Label
        {
            Text      = $"Room: {roomName}  |  Code: {roomCode}  |  You: {username}{encTag}",
            Dock      = DockStyle.Top,
            Height    = 24,
            ForeColor = Color.DimGray,
            Font      = new Font("Segoe UI", 9)
        };

        // ---- Chat area ----
        _chatArea = new RichTextBox
        {
            Dock      = DockStyle.Fill,
            ReadOnly  = true,
            BackColor = Color.White,
            Font      = new Font("Consolas", 10),
            ScrollBars = RichTextBoxScrollBars.Vertical,
            WordWrap  = true
        };

        // ---- Input row ----
        _inputField = new TextBox { Dock = DockStyle.Fill };
        _sendBtn    = new Button  { Text = "Send",  Width = 72, Dock = DockStyle.Right };
        _leaveBtn   = new Button  { Text = "Leave", Width = 72, Dock = DockStyle.Right };

        var inputRow = new Panel { Dock = DockStyle.Bottom, Height = 34, Padding = new Padding(0, 4, 0, 0) };
        inputRow.Controls.Add(_inputField);
        inputRow.Controls.Add(_sendBtn);
        inputRow.Controls.Add(_leaveBtn);

        Controls.Add(_chatArea);
        Controls.Add(inputRow);
        Controls.Add(header);

        _sendBtn.Click         += async (_, _) => await SendMessageAsync();
        _leaveBtn.Click        += async (_, _) => await LeaveAsync();
        _inputField.KeyDown    += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await SendMessageAsync(); }
        };

        _ = LoadHistoryAndConnectAsync();
    }

    private async Task LoadHistoryAndConnectAsync()
    {
        try
        {
            var messages = await new ApiClient(_serverUrl).GetMessagesAsync(_roomCode);
            foreach (var msg in messages)
            {
                string time    = DateTime.Parse(msg.SentAt).ToLocalTime().ToString(TimeFmt);
                string content = DecryptOrFallback(msg.Content);
                AppendLine($"[{time}] [{msg.Sender}] {content}");
            }
        }
        catch (Exception ex)
        {
            AppendLine($"** Could not load history: {ex.Message}");
        }

        await ConnectWebSocketAsync();
    }

    private async Task ConnectWebSocketAsync()
    {
        string wsUrl = _serverUrl
            .Replace("https://", "wss://")
            .Replace("http://",  "ws://")
            + "/ws/websocket";
        try
        {
            _stomp = new StompClient(wsUrl, _roomCode, _username,
                onMessage:   (sender, content) => SafeInvoke(() =>
                    AppendLine($"[{sender}] {DecryptOrFallback(content)}")),
                onUserEvent: evt => SafeInvoke(() =>
                    AppendLine($"* {evt}")));

            await _stomp.ConnectAsync();
        }
        catch (Exception ex)
        {
            AppendLine($"** WebSocket error: {ex.Message}");
        }
    }

    private async Task SendMessageAsync()
    {
        string text = _inputField.Text.Trim();
        if (string.IsNullOrEmpty(text) || _stomp == null) return;
        _inputField.Clear();
        try
        {
            string payload = _crypto != null ? _crypto.Encrypt(text) : text;
            await _stomp.SendChatMessageAsync(payload);
        }
        catch (Exception ex)
        {
            AppendLine($"** Send failed: {ex.Message}");
        }
    }

    private async Task LeaveAsync()
    {
        if (_stomp != null)
        {
            await _stomp.LeaveRoomAsync();
            _stomp.Dispose();
            _stomp = null;
        }
        _onLeave();
    }

    private string DecryptOrFallback(string content)
    {
        if (_crypto == null) return content;
        return _crypto.Decrypt(content) ?? "[could not decrypt]";
    }

    private void AppendLine(string line)
    {
        _chatArea.AppendText(line + Environment.NewLine);
        _chatArea.ScrollToCaret();
    }

    private void SafeInvoke(Action action)
    {
        if (IsDisposed) return;
        if (InvokeRequired) Invoke(action);
        else action();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _stomp?.Dispose();
            _stomp = null;
        }
        base.Dispose(disposing);
    }
}
