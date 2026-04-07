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
    private readonly Panel   _contentPanel;
    private readonly Control _scrollDownBtn;
    private readonly TextBox _inputField;

    private readonly List<(Msg msg, MessageBubble bubble)> _rows = new();
    private readonly HashSet<string> _historyFingerprints = new();
    private int          _nextTop  = 12;
    private int          _scrollY  = 0;
    private StompClient? _stomp;
    private IMessageFilter? _wheelFilter;

    private int  MaxScroll  => Math.Max(0, _contentPanel.Height - _scrollPanel.ClientHeight);
    private bool IsAtBottom() => _scrollY >= MaxScroll - 80;

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
        header.SizeChanged += (_, _) => { };

        // ── Scroll panel (outer, clips, no scrollbars) ────────────────
        _scrollPanel = new DoubleBufferedPanel
        {
            Dock      = DockStyle.Fill,
            BackColor = Theme.ChatBg
        };

        // ── Content panel (inner, moves up/down for scrolling) ────────
        _contentPanel = new Panel
        {
            BackColor = Theme.ChatBg,
            Left      = 0,
            Top       = 0,
            Padding   = Padding.Empty
        };
        _scrollPanel.Controls.Add(_contentPanel);

        // ── Scroll-down overlay button ────────────────────────────────
        _scrollDownBtn = new ScrollDownButton { Visible = false };
        _scrollDownBtn.Click += (_, _) => ScrollToBottom();
        _scrollPanel.Controls.Add(_scrollDownBtn);
        _scrollDownBtn.BringToFront();

        _scrollPanel.Resize += (_, _) =>
        {
            _contentPanel.Width = _scrollPanel.ClientWidth;
            Relayout();
        };

        // Mouse wheel: intercept WM_MOUSEWHEEL for the scroll area regardless of focus
        _wheelFilter = new WheelMessageFilter(_scrollPanel, DoScroll);
        Application.AddMessageFilter(_wheelFilter);

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

        var inputWrap = new Panel
        {
            Dock      = DockStyle.Fill,
            BackColor = Theme.HeaderBg
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

    // ── Scrolling ─────────────────────────────────────────────────────

    // delta: positive = wheel up (scroll toward top), negative = wheel down
    private void DoScroll(int delta)
    {
        int change = -(delta / 120) * 60;
        _scrollY = Math.Clamp(_scrollY + change, 0, MaxScroll);
        _contentPanel.Top = -_scrollY;
        UpdateScrollDownButton();
    }

    private void ScrollToBottom()
    {
        _scrollY = MaxScroll;
        _contentPanel.Top = -_scrollY;
        UpdateScrollDownButton();
    }

    private void UpdateScrollDownButton()
    {
        bool show = MaxScroll > 50 && !IsAtBottom();
        _scrollDownBtn.Visible = show;
        if (show)
        {
            _scrollDownBtn.Left = _scrollPanel.ClientWidth  - _scrollDownBtn.Width  - 16;
            _scrollDownBtn.Top  = _scrollPanel.ClientHeight - _scrollDownBtn.Height - 16;
            _scrollDownBtn.BringToFront();
        }
    }

    // ── Messages ──────────────────────────────────────────────────────

    private void AddMessage(string sender, string content, string time,
                            bool isOwn, bool animate)
    {
        int w      = Math.Max(_contentPanel.ClientSize.Width, _scrollPanel.ClientWidth);
        var msg    = new Msg(sender, content, time, isOwn);
        var bubble = new MessageBubble(sender, content, time, isOwn, w);
        bubble.Top  = _nextTop;
        _nextTop   += bubble.Height;

        _rows.Add((msg, bubble));
        _contentPanel.Height = _nextTop + 12;
        _contentPanel.Controls.Add(bubble);

        if (animate) bubble.AnimateIn();

        // Always scroll to bottom for own messages; otherwise only if already near bottom
        if (isOwn || IsAtBottom())
            ScrollToBottom();
        else
            UpdateScrollDownButton();
    }

    private void Relayout()
    {
        _scrollPanel.SuspendLayout();
        _contentPanel.Width = _scrollPanel.ClientWidth;
        _nextTop = 12;
        foreach (var (_, bubble) in _rows)
        {
            bubble.Relayout(_contentPanel.ClientSize.Width);
            bubble.Top  = _nextTop;
            _nextTop   += bubble.Height;
        }
        _contentPanel.Height = _nextTop + 12;
        _scrollPanel.ResumeLayout();
        ScrollToBottom();
    }

    // ── Network ───────────────────────────────────────────────────────

    private async Task LoadAndConnectAsync()
    {
        try
        {
            var history = await new ApiClient(_serverUrl).GetMessagesAsync(_roomCode);
            foreach (var m in history)
            {
                string t     = DateTime.Parse(m.SentAt).ToLocalTime().ToString("HH:mm");
                string plain = Decrypt(m.Content);
                // Register fingerprint so WS echo of this message is skipped
                _historyFingerprints.Add(MakeFingerprint(m.Sender, m.Content));
                AddMessage(m.Sender, plain, t, m.Sender == _username, false);
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
                    // Skip if this is an echo of a history message
                    if (_historyFingerprints.Remove(MakeFingerprint(sender, content))) return;

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

    private static string MakeFingerprint(string sender, string content)
        => $"{sender}\x00{content}";

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
            Width     = _contentPanel.ClientSize.Width,
            Top       = _nextTop,
            Left      = 0,
            TextAlign = ContentAlignment.MiddleCenter
        };
        _nextTop += 26;
        _contentPanel.Height = _nextTop + 12;
        _contentPanel.Controls.Add(lbl);
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
        if (disposing)
        {
            if (_wheelFilter != null)
            {
                Application.RemoveMessageFilter(_wheelFilter);
                _wheelFilter = null;
            }
            _stomp?.Dispose();
            _stomp = null;
        }
        base.Dispose(disposing);
    }
}

file sealed class DoubleBufferedPanel : Panel
{
    public DoubleBufferedPanel() => DoubleBuffered = true;
}

file sealed class ScrollDownButton : Control
{
    public ScrollDownButton()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Size      = new Size(36, 36);
        Cursor    = Cursors.Hand;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // Semi-transparent dark circle
        using var bg = new SolidBrush(Color.FromArgb(200, 42, 57, 73));
        g.FillEllipse(bg, 1, 1, Width - 3, Height - 3);

        // Subtle border
        using var border = new Pen(Color.FromArgb(100, 154, 167, 178), 1f);
        g.DrawEllipse(border, 1, 1, Width - 3, Height - 3);

        // Down-chevron arrow
        using var arrow = new Pen(Color.FromArgb(230, 230, 235, 240), 2f)
        {
            StartCap = System.Drawing.Drawing2D.LineCap.Round,
            EndCap   = System.Drawing.Drawing2D.LineCap.Round,
            LineJoin = System.Drawing.Drawing2D.LineJoin.Round
        };
        int cx = Width / 2, cy = Height / 2;
        g.DrawLines(arrow, new PointF[] {
            new(cx - 6, cy - 2),
            new(cx,     cy + 4),
            new(cx + 6, cy - 2)
        });
    }
}

/// <summary>
/// Routes WM_MOUSEWHEEL to the scroll panel whenever the cursor is inside it,
/// regardless of which control has keyboard focus.
/// </summary>
file sealed class WheelMessageFilter : IMessageFilter
{
    private const int WM_MOUSEWHEEL = 0x020A;
    private readonly Control     _target;
    private readonly Action<int> _onScroll;

    public WheelMessageFilter(Control target, Action<int> onScroll)
    {
        _target   = target;
        _onScroll = onScroll;
    }

    public bool PreFilterMessage(ref Message m)
    {
        if (m.Msg != WM_MOUSEWHEEL || _target.IsDisposed) return false;
        try
        {
            var bounds = _target.RectangleToScreen(_target.ClientRectangle);
            if (!bounds.Contains(Cursor.Position)) return false;
            int delta = (short)((m.WParam.ToInt64() >> 16) & 0xFFFF);
            _onScroll(delta);
            return true; // consume — prevent any other scroll handler
        }
        catch { return false; }
    }
}
