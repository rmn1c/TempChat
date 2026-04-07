using System.Drawing.Drawing2D;
using TempChat.Services;

namespace TempChat.UI;

public sealed class ChatPanel : Panel, IDisposable
{
    private record Msg(string Sender, string Content, string Time, bool IsOwn);

    private readonly string         _serverUrl;
    private readonly string         _roomCode;
    private readonly string         _username;
    private readonly Action         _onLeave;
    private readonly CryptoService? _crypto;

    private readonly Panel   _scrollPanel;
    private readonly TextBox _inputField;

    private readonly List<(Msg msg, MessageBubble bubble)> _rows = new();
    private int          _nextTop = 12;
    private StompClient? _stomp;

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

        // ── Header ────────────────────────────────────────────────────
        string encBadge = _crypto != null ? " 🔒" : "";
        var header = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 68,
            BackColor = Theme.HeaderBg,
            Padding   = new Padding(20, 10, 16, 10)
        };
        header.Paint += (_, e) =>
        {
            using var pen = new Pen(Theme.Border, 1);
            e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1);
        };

        // Small, compact leave button — vertically centered manually
        var leaveBtn = new RoundButton("Leave", primary: false)
        {
            Width  = 60,
            Height = 26
        };
        leaveBtn.Anchor = AnchorStyles.Right | AnchorStyles.Top;
        header.SizeChanged += (_, _) =>
        {
            leaveBtn.Left = header.Width - header.Padding.Right - leaveBtn.Width;
            leaveBtn.Top  = (header.Height - leaveBtn.Height) / 2;
        };

        var roomLbl = new Label
        {
            Text      = roomName + encBadge,
            Font      = Theme.HeaderFont,
            ForeColor = Theme.Text,
            Dock      = DockStyle.Top,
            Height    = 28,
            TextAlign = ContentAlignment.BottomLeft
        };

        var codeLbl = new Label
        {
            Text      = $"Code: {roomCode}  ·  {username}",
            Font      = Theme.SubFont,
            ForeColor = Theme.SubText,
            Dock      = DockStyle.Bottom,
            Height    = 20,
            TextAlign = ContentAlignment.TopLeft,
            Cursor    = Cursors.Hand
        };
        codeLbl.Click += (_, _) =>
        {
            Clipboard.SetText(roomCode);
            string orig      = codeLbl.Text;
            Color  origColor = codeLbl.ForeColor;
            codeLbl.Text      = "✓  Code copied!";
            codeLbl.ForeColor = Theme.Accent;
            var timer = new System.Windows.Forms.Timer { Interval = 1800 };
            timer.Tick += (_, _) =>
            {
                codeLbl.Text      = orig;
                codeLbl.ForeColor = origColor;
                timer.Stop(); timer.Dispose();
            };
            timer.Start();
        };

        header.Controls.Add(roomLbl);
        header.Controls.Add(codeLbl);
        header.Controls.Add(leaveBtn);
        // Trigger manual layout immediately
        header.SizeChanged += (_, _) => { };

        // ── Scroll panel ──────────────────────────────────────────────
        _scrollPanel = new DoubleBufferedPanel
        {
            Dock       = DockStyle.Fill,
            BackColor  = Theme.ChatBg,
            AutoScroll = true,
            Padding    = new Padding(0, 6, 0, 6)
        };
        _scrollPanel.Resize += (_, _) => Relayout();

        // ── Input bar ─────────────────────────────────────────────────
        var inputBar = new Panel
        {
            Dock      = DockStyle.Bottom,
            Height    = 68,
            BackColor = Theme.HeaderBg,
            Padding   = new Padding(14, 14, 14, 14)
        };
        inputBar.Paint += (_, e) =>
        {
            using var pen = new Pen(Theme.Border, 1);
            e.Graphics.DrawLine(pen, 0, 0, inputBar.Width, 0);
        };

        // Input field pill — BackColor matches parent so rounded corners don't bleed
        var inputWrap = new Panel
        {
            Dock      = DockStyle.Fill,
            BackColor = Theme.HeaderBg   // ← must match parent to hide rect corners
        };
        inputWrap.Paint += (_, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.HeaderBg);
            var r = new Rectangle(0, 0, inputWrap.Width - 1, inputWrap.Height - 1);
            using var path   = Pill(r);
            using var fill   = new SolidBrush(Theme.InputBg);
            using var border = new Pen(Theme.Border, 1);
            g.FillPath(fill, path);
            g.DrawPath(border, path);
        };

        _inputField = new TextBox
        {
            BorderStyle = BorderStyle.None,
            BackColor   = Theme.InputBg,
            ForeColor   = Theme.SubText,
            Font        = Theme.ChatFont,
            Text        = "Type a message…",
            Anchor      = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
        };
        inputWrap.SizeChanged += (_, _) =>
        {
            _inputField.Left  = 16;
            _inputField.Width = inputWrap.Width - 32;
            _inputField.Top   = (inputWrap.Height - _inputField.Height) / 2;
        };
        _inputField.GotFocus += (_, _) =>
        {
            if (_inputField.Text == "Type a message…")
            { _inputField.Text = ""; _inputField.ForeColor = Theme.Text; }
        };
        _inputField.LostFocus += (_, _) =>
        {
            if (string.IsNullOrEmpty(_inputField.Text))
            { _inputField.Text = "Type a message…"; _inputField.ForeColor = Theme.SubText; }
        };
        inputWrap.Controls.Add(_inputField);

        // Small gap between pill and send button
        var sendGap = new Panel { Width = 8, Dock = DockStyle.Right, BackColor = Theme.HeaderBg };

        var sendBtn = new RoundButton("Send")
        {
            Dock  = DockStyle.Right,
            Width = 68
        };

        inputBar.Controls.Add(inputWrap);
        inputBar.Controls.Add(sendGap);
        inputBar.Controls.Add(sendBtn);

        // ── Assemble ──────────────────────────────────────────────────
        Controls.Add(_scrollPanel);
        Controls.Add(inputBar);
        Controls.Add(header);

        sendBtn.Click      += async (_, _) => await SendAsync();
        leaveBtn.Click     += async (_, _) => await LeaveAsync();
        _inputField.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await SendAsync(); }
        };

        _ = LoadAndConnectAsync();
    }

    // ── Messages ──────────────────────────────────────────────────────

    private void AddMessage(string sender, string content, string time,
                            bool isOwn, bool animate)
    {
        var msg    = new Msg(sender, content, time, isOwn);
        var bubble = new MessageBubble(sender, content, time, isOwn,
                                       _scrollPanel.ClientSize.Width);
        bubble.Top  = _nextTop;
        _nextTop   += bubble.Height;

        _rows.Add((msg, bubble));
        _scrollPanel.AutoScrollMinSize = new Size(0, _nextTop + 12);
        _scrollPanel.Controls.Add(bubble);

        if (animate) bubble.AnimateIn();
        ScrollToBottom();
    }

    private void Relayout()
    {
        _scrollPanel.SuspendLayout();
        _nextTop = 12;
        foreach (var (_, bubble) in _rows)
        {
            bubble.Relayout(_scrollPanel.ClientSize.Width);
            bubble.Top  = _nextTop;
            _nextTop   += bubble.Height;
        }
        _scrollPanel.AutoScrollMinSize = new Size(0, _nextTop + 12);
        _scrollPanel.ResumeLayout();
        ScrollToBottom();
    }

    private void ScrollToBottom() =>
        _scrollPanel.AutoScrollPosition = new Point(0, _nextTop);

    // ── Network ───────────────────────────────────────────────────────

    private async Task LoadAndConnectAsync()
    {
        try
        {
            var history = await new ApiClient(_serverUrl).GetMessagesAsync(_roomCode);
            foreach (var m in history)
            {
                string t = DateTime.Parse(m.SentAt).ToLocalTime().ToString("HH:mm");
                AddMessage(m.Sender, Decrypt(m.Content), t, m.Sender == _username, false);
            }
        }
        catch (Exception ex) { AddSystemLine($"Could not load history: {ex.Message}"); }

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
                    string plain = Decrypt(content);
                    string t     = DateTime.Now.ToString("HH:mm");
                    AddMessage(sender, plain, t, sender == _username, true);

                    if (sender != _username)
                        ToastService.NotifyIfUnfocused(sender, plain);
                }),
                onUserEvent: evt => SafeInvoke(() => AddSystemLine(evt)));

            await _stomp.ConnectAsync();
        }
        catch (Exception ex) { AddSystemLine($"WebSocket error: {ex.Message}"); }
    }

    private async Task SendAsync()
    {
        string text = _inputField.Text;
        if (text == "Type a message…" || string.IsNullOrWhiteSpace(text) || _stomp == null) return;
        _inputField.Text = "";
        try
        {
            string payload = _crypto != null ? _crypto.Encrypt(text.Trim()) : text.Trim();
            await _stomp.SendChatMessageAsync(payload);
        }
        catch (Exception ex) { AddSystemLine($"Send failed: {ex.Message}"); }
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

    // ── Helpers ───────────────────────────────────────────────────────

    private string Decrypt(string content)
    {
        if (_crypto == null) return content;
        return _crypto.Decrypt(content) ?? "[could not decrypt]";
    }

    private void AddSystemLine(string line)
    {
        var lbl = new Label
        {
            Text      = "— " + line + " —",
            ForeColor = Theme.SubText,
            Font      = Theme.SubFont,
            AutoSize  = false,
            Height    = 22,
            Width     = _scrollPanel.ClientSize.Width,
            Top       = _nextTop,
            Left      = 0,
            TextAlign = ContentAlignment.MiddleCenter
        };
        _nextTop += 26;
        _scrollPanel.AutoScrollMinSize = new Size(0, _nextTop + 12);
        _scrollPanel.Controls.Add(lbl);
        ScrollToBottom();
    }

    private void SafeInvoke(Action action)
    {
        if (IsDisposed) return;
        if (InvokeRequired) Invoke(action);
        else action();
    }

    private static GraphicsPath Pill(Rectangle r)
    {
        int rad = r.Height;
        var p   = new GraphicsPath();
        p.AddArc(r.X,           r.Y,            rad, rad, 180, 90);
        p.AddArc(r.Right - rad, r.Y,            rad, rad, 270, 90);
        p.AddArc(r.Right - rad, r.Bottom - rad, rad, rad,   0, 90);
        p.AddArc(r.X,           r.Bottom - rad, rad, rad,  90, 90);
        p.CloseFigure();
        return p;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _stomp?.Dispose(); _stomp = null; }
        base.Dispose(disposing);
    }
}

file sealed class DoubleBufferedPanel : Panel
{
    public DoubleBufferedPanel() => DoubleBuffered = true;
}
