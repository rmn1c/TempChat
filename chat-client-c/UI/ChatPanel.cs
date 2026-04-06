using TempChat.Services;

namespace TempChat.UI;

public sealed class ChatPanel : Panel, IDisposable
{
    // Stored so we can relayout on resize
    private record Msg(string Sender, string Content, string Time, bool IsOwn);

    private readonly string        _serverUrl;
    private readonly string        _roomCode;
    private readonly string        _username;
    private readonly Action        _onLeave;
    private readonly CryptoService? _crypto;

    private readonly Panel   _scrollPanel;
    private readonly TextBox _inputField;
    private readonly Button  _sendBtn;
    private readonly Button  _leaveBtn;

    private readonly List<Msg> _stored  = new();
    private int                _nextTop = 10;
    private StompClient?       _stomp;

    public ChatPanel(string serverUrl, string roomCode, string username,
                     string roomName, string roomPassword, Action onLeave)
    {
        _serverUrl = serverUrl;
        _roomCode  = roomCode;
        _username  = username;
        _onLeave   = onLeave;
        BackColor  = Theme.ChatBg;

        if (!string.IsNullOrEmpty(roomPassword))
            try { _crypto = new CryptoService(roomPassword, roomCode); } catch { }

        // ── Header ────────────────────────────────────────────────
        string enc = _crypto != null ? "  🔒" : "  ⚠";
        var header = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 52,
            BackColor = Theme.HeaderBg,
            Padding   = new Padding(16, 0, 12, 0)
        };
        // subtle bottom border
        header.Paint += (_, e) =>
        {
            using var pen = new Pen(Theme.Border, 1);
            e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1);
        };

        var roomLabel = new Label
        {
            Text      = roomName + enc,
            Font      = Theme.HeaderFont,
            ForeColor = Theme.Text,
            AutoSize  = false,
            Dock      = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
        var codeLabel = new Label
        {
            Text      = "Code: " + roomCode + "   You: " + username,
            Font      = Theme.SubFont,
            ForeColor = Theme.SubText,
            AutoSize  = false,
            Dock      = DockStyle.Bottom,
            Height    = 18,
            TextAlign = ContentAlignment.BottomLeft
        };
        header.Controls.Add(roomLabel);
        header.Controls.Add(codeLabel);

        _leaveBtn = Theme.MakeButton("Leave", primary: false);
        _leaveBtn.Dock   = DockStyle.Right;
        _leaveBtn.Width  = 72;
        _leaveBtn.Margin = new Padding(0, 8, 0, 8);
        header.Controls.Add(_leaveBtn);

        // ── Scroll area ───────────────────────────────────────────
        _scrollPanel = new DoubleBufferedPanel
        {
            Dock       = DockStyle.Fill,
            BackColor  = Theme.ChatBg,
            AutoScroll = true
        };
        _scrollPanel.Resize += (_, _) => Relayout();

        // ── Input bar ─────────────────────────────────────────────
        var inputBar = new Panel
        {
            Dock      = DockStyle.Bottom,
            Height    = 58,
            BackColor = Theme.HeaderBg,
            Padding   = new Padding(12, 10, 12, 10)
        };
        inputBar.Paint += (_, e) =>
        {
            using var pen = new Pen(Theme.Border, 1);
            e.Graphics.DrawLine(pen, 0, 0, inputBar.Width, 0);
        };

        _inputField = Theme.MakeTextBox("Type a message…");
        _inputField.Dock = DockStyle.Fill;

        var inputWrapper = new Panel
        {
            Dock      = DockStyle.Fill,
            BackColor = Theme.InputBg,
            Padding   = new Padding(10, 0, 10, 0)
        };
        // Rounded visual via Paint
        inputWrapper.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var r = new Rectangle(0, 0, inputWrapper.Width - 1, inputWrapper.Height - 1);
            using var path = RoundRect(r, 20);
            using var fill = new SolidBrush(Theme.InputBg);
            e.Graphics.FillPath(fill, path);
            using var pen = new Pen(Theme.Border, 1);
            e.Graphics.DrawPath(pen, path);
        };
        _inputField.Dock = DockStyle.Fill;
        inputWrapper.Controls.Add(_inputField);

        _sendBtn = Theme.MakeButton("Send");
        _sendBtn.Dock  = DockStyle.Right;
        _sendBtn.Width = 72;

        inputBar.Controls.Add(inputWrapper);
        inputBar.Controls.Add(_sendBtn);

        // ── Wire up ──────────────────────────────────────────────
        Controls.Add(_scrollPanel);
        Controls.Add(inputBar);
        Controls.Add(header);

        _sendBtn.Click      += async (_, _) => await SendAsync();
        _leaveBtn.Click     += async (_, _) => await LeaveAsync();
        _inputField.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                await SendAsync();
            }
        };

        _ = LoadAndConnectAsync();
    }

    // ── Message handling ─────────────────────────────────────────

    private void AddMessage(string sender, string content, string time,
                            bool isOwn, bool animate)
    {
        _stored.Add(new Msg(sender, content, time, isOwn));
        PlaceBubble(new Msg(sender, content, time, isOwn), animate);
        ScrollToBottom();
    }

    private void PlaceBubble(Msg msg, bool animate)
    {
        var bubble = new MessageBubble(
            msg.Sender, msg.Content, msg.Time, msg.IsOwn,
            _scrollPanel.ClientSize.Width);

        bubble.Top  = _nextTop;
        _nextTop   += bubble.Height + 6;

        _scrollPanel.AutoScrollMinSize = new Size(0, _nextTop + 10);
        _scrollPanel.Controls.Add(bubble);

        if (animate) bubble.AnimateIn();
    }

    private void Relayout()
    {
        _scrollPanel.SuspendLayout();
        _scrollPanel.Controls.Clear();
        _nextTop = 10;
        foreach (var m in _stored) PlaceBubble(m, false);
        _scrollPanel.ResumeLayout();
        ScrollToBottom();
    }

    private void ScrollToBottom() =>
        _scrollPanel.AutoScrollPosition = new Point(0, _nextTop);

    // ── Network ──────────────────────────────────────────────────

    private async Task LoadAndConnectAsync()
    {
        try
        {
            var msgs = await new ApiClient(_serverUrl).GetMessagesAsync(_roomCode);
            foreach (var m in msgs)
            {
                string t = DateTime.Parse(m.SentAt).ToLocalTime().ToString("HH:mm");
                AddMessage(m.Sender, Decrypt(m.Content), t, m.Sender == _username, false);
            }
        }
        catch (Exception ex)
        {
            AddSystemLine($"Could not load history: {ex.Message}");
        }
        await ConnectWsAsync();
    }

    private async Task ConnectWsAsync()
    {
        string wsUrl = _serverUrl
            .Replace("https://", "wss://")
            .Replace("http://",  "ws://")
            + "/ws/websocket";
        try
        {
            _stomp = new StompClient(wsUrl, _roomCode, _username,
                onMessage: (sender, content) => SafeInvoke(() =>
                {
                    string t = DateTime.Now.ToString("HH:mm");
                    AddMessage(sender, Decrypt(content), t, sender == _username, true);
                }),
                onUserEvent: evt => SafeInvoke(() => AddSystemLine(evt)));
            await _stomp.ConnectAsync();
        }
        catch (Exception ex)
        {
            AddSystemLine($"WebSocket error: {ex.Message}");
        }
    }

    private async Task SendAsync()
    {
        string text = _inputField.Text.Trim();
        // Clear placeholder
        if (text == (string?)_inputField.Tag || string.IsNullOrEmpty(text) || _stomp == null) return;
        _inputField.Text = "";
        try
        {
            string payload = _crypto != null ? _crypto.Encrypt(text) : text;
            await _stomp.SendChatMessageAsync(payload);
        }
        catch (Exception ex)
        {
            AddSystemLine($"Send failed: {ex.Message}");
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

    // ── Helpers ──────────────────────────────────────────────────

    private string Decrypt(string content)
    {
        if (_crypto == null) return content;
        return _crypto.Decrypt(content) ?? "[could not decrypt]";
    }

    private void AddSystemLine(string line)
    {
        var lbl = new Label
        {
            Text      = line,
            ForeColor = Theme.SubText,
            Font      = Theme.SubFont,
            AutoSize  = false,
            Height    = 20,
            Width     = _scrollPanel.ClientSize.Width,
            Top       = _nextTop,
            Left      = 0,
            TextAlign = ContentAlignment.MiddleCenter
        };
        _nextTop += 24;
        _scrollPanel.AutoScrollMinSize = new Size(0, _nextTop + 10);
        _scrollPanel.Controls.Add(lbl);
        ScrollToBottom();
    }

    private void SafeInvoke(Action action)
    {
        if (IsDisposed) return;
        if (InvokeRequired) Invoke(action);
        else action();
    }

    private static System.Drawing.Drawing2D.GraphicsPath RoundRect(Rectangle r, int radius)
    {
        var p = new System.Drawing.Drawing2D.GraphicsPath();
        p.AddArc(r.X,               r.Y,               radius * 2, radius * 2, 180, 90);
        p.AddArc(r.Right - radius * 2, r.Y,             radius * 2, radius * 2, 270, 90);
        p.AddArc(r.Right - radius * 2, r.Bottom - radius * 2, radius * 2, radius * 2, 0, 90);
        p.AddArc(r.X,               r.Bottom - radius * 2, radius * 2, radius * 2, 90, 90);
        p.CloseFigure();
        return p;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _stomp?.Dispose(); _stomp = null; }
        base.Dispose(disposing);
    }
}

// Exposes the protected DoubleBuffered property
file sealed class DoubleBufferedPanel : Panel
{
    public DoubleBufferedPanel() => DoubleBuffered = true;
}
