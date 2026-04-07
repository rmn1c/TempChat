using System.Drawing.Drawing2D;
using TempChat.Services;

namespace TempChat.UI;

public sealed class LoginPanel : Panel
{
    public delegate void JoinCallback(string serverUrl, string roomCode, string username,
                                      string roomName, string roomPassword);

    private readonly TextBox     _hostField;
    private readonly TextBox     _portField;
    private readonly TextBox     _usernameField;
    private readonly TextBox     _roomPasswordField;
    private readonly TextBox     _roomCodeField;
    private readonly TextBox     _newRoomNameField;
    private readonly RoundButton _joinBtn;
    private readonly RoundButton _createBtn;
    private readonly Label       _statusLabel;
    private readonly JoinCallback _callback;

    public LoginPanel(string defaultHost, string defaultPort, JoinCallback callback)
    {
        _callback = callback;
        BackColor = Theme.Background;
        Padding   = new Padding(0);

        // ── Centered card ─────────────────────────────────────────────
        var center = new Panel
        {
            Width     = 380,
            BackColor = Theme.Surface,
            Padding   = new Padding(32, 28, 32, 28)
        };
        center.Paint += PaintCard;

        // ── Title ─────────────────────────────────────────────────────
        var title = new Label
        {
            Text      = "TempChat",
            Font      = Theme.TitleFont,
            ForeColor = Theme.Purple,
            Dock      = DockStyle.Top,
            Height    = 52,
            TextAlign = ContentAlignment.MiddleCenter
        };

        var sub = new Label
        {
            Text      = "Private · Encrypted · Ephemeral",
            Font      = Theme.SubFont,
            ForeColor = Theme.SubText,
            Dock      = DockStyle.Top,
            Height    = 22,
            TextAlign = ContentAlignment.TopCenter
        };

        // ── Fields ────────────────────────────────────────────────────
        _hostField             = Theme.MakeTextBox();
        _hostField.Text        = defaultHost;
        _hostField.ForeColor   = Theme.Text;

        _portField             = Theme.MakeTextBox();
        _portField.Text        = defaultPort;
        _portField.ForeColor   = Theme.Text;

        _usernameField     = Theme.MakeTextBox("Username");
        _roomPasswordField = Theme.MakeTextBox("Room password (for encryption)", password: true);
        _roomCodeField     = Theme.MakeTextBox("Room code");
        _newRoomNameField  = Theme.MakeTextBox("New room name");

        var serverRow = MakeServerBubble(_hostField, _portField);

        _joinBtn   = Theme.MakeButton("Join Room");
        _createBtn = Theme.MakeButton("Create Room", primary: false);

        _statusLabel = new Label
        {
            Text      = " ",
            ForeColor = Color.FromArgb(220, 80, 80),
            Font      = Theme.SubFont,
            Dock      = DockStyle.Top,
            Height    = 22,
            TextAlign = ContentAlignment.MiddleCenter
        };

        var sepLabel = new Label
        {
            Text      = "────────── or ──────────",
            ForeColor = Theme.SubText,
            Font      = Theme.SubFont,
            Dock      = DockStyle.Top,
            Height    = 24,
            TextAlign = ContentAlignment.MiddleCenter
        };

        var hint = new Label
        {
            Text      = "Room password enables end-to-end encryption",
            Font      = new Font("Segoe UI", 8.5f, FontStyle.Italic),
            ForeColor = Theme.SubText,
            Dock      = DockStyle.Top,
            Height    = 20,
            TextAlign = ContentAlignment.MiddleCenter
        };

        // Build top-to-bottom (added in reverse because Dock=Top stacks upward)
        var form = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface };
        var controls = new Control[]
        {
            _statusLabel,
            Spacer(6),
            _createBtn,
            Spacer(4),
            _joinBtn,
            Spacer(6),
            WrapBubble(_newRoomNameField),
            WrapBubble(_roomCodeField),
            sepLabel,
            WrapBubble(_roomPasswordField),
            hint,
            Spacer(4),
            WrapBubble(_usernameField),
            Spacer(4),
            serverRow,
            WrapLabel(sub),
            WrapLabel(title),
            Spacer(4),
        };
        foreach (var c in controls) { c.Dock = DockStyle.Top; form.Controls.Add(c); }
        form.Controls.SetChildIndex(controls[^1], 0);

        center.Controls.Add(form);

        Resize += (_, _) => PositionCenter(center);
        Controls.Add(center);
        PositionCenter(center);

        _joinBtn.Click   += async (_, _) => await HandleJoinAsync();
        _createBtn.Click += async (_, _) => await HandleCreateAsync();
    }

    private void PositionCenter(Panel center)
    {
        center.Height = Math.Min(Height - 60, 640);
        center.Left   = (Width  - center.Width)  / 2;
        center.Top    = (Height - center.Height) / 2;
    }

    // ── Bubble field wrapper — rounded corners, shadow, vertically centred TB ──

    private static Panel WrapBubble(TextBox tb)
    {
        const int bubbleH = 36;
        const int radius  = 10;

        var outer = new Panel { Height = bubbleH + 10, BackColor = Theme.Surface };

        outer.Paint += (_, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Drop shadow — two offset layers
            using var sh1 = new SolidBrush(Color.FromArgb(38, 0, 0, 0));
            using var sp1 = RoundRect(new Rectangle(2, 7, outer.Width - 5, bubbleH), radius);
            g.FillPath(sh1, sp1);

            using var sh2 = new SolidBrush(Color.FromArgb(20, 0, 0, 0));
            using var sp2 = RoundRect(new Rectangle(1, 5, outer.Width - 3, bubbleH), radius);
            g.FillPath(sh2, sp2);

            // Bubble fill + border
            var bubbleRect = new Rectangle(0, 2, outer.Width - 2, bubbleH);
            using var path   = RoundRect(bubbleRect, radius);
            using var fill   = new SolidBrush(Theme.InputBg);
            using var border = new Pen(Theme.Border, 1f);
            g.FillPath(fill, path);
            g.DrawPath(border, path);
        };

        tb.BackColor = Theme.InputBg;
        // Do NOT dock the textbox — manually centre it inside the bubble
        tb.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;

        void Layout()
        {
            if (outer.Width <= 24) return;
            tb.Left  = 12;
            tb.Width = outer.Width - 24;
            tb.Top   = 2 + (bubbleH - tb.Height) / 2;
        }

        outer.SizeChanged += (_, _) => Layout();
        outer.Controls.Add(tb);
        Layout();
        return outer;
    }

    // ── Combined host+port bubble with a thin divider ─────────────────

    private static Panel MakeServerBubble(TextBox hostField, TextBox portField)
    {
        const int bubbleH = 36;
        const int radius  = 10;
        const int portW   = 76;

        var outer = new Panel { Height = bubbleH + 10, BackColor = Theme.Surface };

        outer.Paint += (_, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using var sh1 = new SolidBrush(Color.FromArgb(38, 0, 0, 0));
            using var sp1 = RoundRect(new Rectangle(2, 7, outer.Width - 5, bubbleH), radius);
            g.FillPath(sh1, sp1);

            using var sh2 = new SolidBrush(Color.FromArgb(20, 0, 0, 0));
            using var sp2 = RoundRect(new Rectangle(1, 5, outer.Width - 3, bubbleH), radius);
            g.FillPath(sh2, sp2);

            var bubbleRect = new Rectangle(0, 2, outer.Width - 2, bubbleH);
            using var path   = RoundRect(bubbleRect, radius);
            using var fill   = new SolidBrush(Theme.InputBg);
            using var border = new Pen(Theme.Border, 1f);
            g.FillPath(fill, path);
            g.DrawPath(border, path);

            // Vertical divider between host and port sections
            int divX = outer.Width - portW - 2;
            using var divPen = new Pen(Theme.Border, 1f);
            g.DrawLine(divPen, divX, 6, divX, bubbleH + 2);
        };

        hostField.BackColor = Theme.InputBg;
        portField.BackColor = Theme.InputBg;
        hostField.Anchor    = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
        portField.Anchor    = AnchorStyles.Right | AnchorStyles.Top;

        void Layout()
        {
            if (outer.Width <= 24) return;
            int divX = outer.Width - portW - 2;
            int tbY  = 2 + (bubbleH - hostField.Height) / 2;

            hostField.Left  = 12;
            hostField.Width = divX - 18;
            hostField.Top   = tbY;

            portField.Left  = divX + 8;
            portField.Width = portW - 16;
            portField.Top   = tbY;
        }

        outer.SizeChanged += (_, _) => Layout();
        outer.Controls.Add(hostField);
        outer.Controls.Add(portField);
        Layout();
        return outer;
    }

    // ── Card background ───────────────────────────────────────────────

    private static void PaintCard(object? sender, PaintEventArgs e)
    {
        if (sender is not Panel p) return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var r = new Rectangle(0, 0, p.Width - 1, p.Height - 1);
        using var path = RoundRect(r, 14);
        using var fill = new SolidBrush(Theme.Surface);
        e.Graphics.FillPath(fill, path);
        using var pen = new Pen(Theme.Border, 1);
        e.Graphics.DrawPath(pen, path);
    }

    // ── Layout helpers ────────────────────────────────────────────────

    /// Wrap a non-input control (label, etc.) in a transparent container.
    private static Panel WrapLabel(Control c)
    {
        var w = new Panel { Height = c is Label l ? l.Height : 38, BackColor = Color.Transparent };
        c.Dock = DockStyle.Fill;
        w.Controls.Add(c);
        return w;
    }

    private static Panel Spacer(int h) => new() { Height = h, BackColor = Color.Transparent };

    private static GraphicsPath RoundRect(Rectangle r, int radius)
    {
        var p = new GraphicsPath();
        p.AddArc(r.X,                  r.Y,                  radius * 2, radius * 2, 180, 90);
        p.AddArc(r.Right - radius * 2, r.Y,                  radius * 2, radius * 2, 270, 90);
        p.AddArc(r.Right - radius * 2, r.Bottom - radius * 2, radius * 2, radius * 2,  0, 90);
        p.AddArc(r.X,                  r.Bottom - radius * 2, radius * 2, radius * 2, 90, 90);
        p.CloseFigure();
        return p;
    }

    // ── Network handlers ──────────────────────────────────────────────

    private async Task HandleJoinAsync()
    {
        string host     = _hostField.Text.Trim();
        string port     = _portField.Text.Trim();
        string username = _usernameField.Text.Trim();
        string code     = _roomCodeField.Text.Trim().ToUpper();
        string password = _roomPasswordField.Text;

        if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(port)) { SetStatus("Server IP and port are required."); return; }
        if (string.IsNullOrEmpty(username) || username == (string?)_usernameField.Tag) { SetStatus("Username is required."); return; }
        if (string.IsNullOrEmpty(code) || code == ((string?)_roomCodeField.Tag)?.ToUpper()) { SetStatus("Room code is required."); return; }

        string server = $"http://{host}:{port}";
        SetBusy(true, "Connecting…");
        try
        {
            var room = await new ApiClient(server).GetRoomAsync(code);
            SetBusy(false, " ");
            _callback(server, room.Code, username, room.Name,
                      password == (string?)_roomPasswordField.Tag ? "" : password);
        }
        catch (Exception ex) { SetBusy(false, $"Error: {Root(ex)}"); }
    }

    private async Task HandleCreateAsync()
    {
        string host     = _hostField.Text.Trim();
        string port     = _portField.Text.Trim();
        string username = _usernameField.Text.Trim();
        string name     = _newRoomNameField.Text.Trim();
        string password = _roomPasswordField.Text;

        if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(port)) { SetStatus("Server IP and port are required."); return; }
        if (string.IsNullOrEmpty(username) || username == (string?)_usernameField.Tag) { SetStatus("Username is required."); return; }
        if (string.IsNullOrEmpty(name) || name == (string?)_newRoomNameField.Tag) { SetStatus("Room name is required."); return; }

        string server = $"http://{host}:{port}";
        SetBusy(true, "Creating room…");
        try
        {
            var room = await new ApiClient(server).CreateRoomAsync(name);
            SetBusy(false, " ");
            _callback(server, room.Code, username, room.Name,
                      password == (string?)_roomPasswordField.Tag ? "" : password);
        }
        catch (Exception ex) { SetBusy(false, $"Error: {Root(ex)}"); }
    }

    private void SetBusy(bool busy, string status)
    {
        _joinBtn.Enabled           = !busy;
        _createBtn.Enabled         = !busy;
        _hostField.Enabled         = !busy;
        _portField.Enabled         = !busy;
        _usernameField.Enabled     = !busy;
        _roomPasswordField.Enabled = !busy;
        _roomCodeField.Enabled     = !busy;
        _newRoomNameField.Enabled  = !busy;
        _statusLabel.Text          = status;
    }

    private void SetStatus(string msg) => _statusLabel.Text = msg;

    private static string Root(Exception ex)
    {
        Exception e = ex;
        while (e.InnerException != null) e = e.InnerException;
        return e.Message ?? ex.GetType().Name;
    }
}
