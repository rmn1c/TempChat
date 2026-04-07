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
        center.Paint += PaintRoundedPanel;

        // ── Title ─────────────────────────────────────────────────────
        var title = new Label
        {
            Text      = "TempChat",
            Font      = Theme.TitleFont,
            ForeColor = Theme.Accent,
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
        _hostField            = Theme.MakeTextBox();
        _hostField.Text       = defaultHost;
        _hostField.ForeColor  = Theme.Text;

        _portField            = Theme.MakeTextBox();
        _portField.Text       = defaultPort;
        _portField.ForeColor  = Theme.Text;

        _usernameField     = Theme.MakeTextBox("Username");
        _roomPasswordField = Theme.MakeTextBox("Room password (for encryption)", password: true);
        _roomCodeField     = Theme.MakeTextBox("Room code");
        _newRoomNameField  = Theme.MakeTextBox("New room name");

        // Host + Port on same row
        var serverRow = new Panel { Dock = DockStyle.Top, Height = 38, Margin = new Padding(0, 0, 0, 6) };
        var hostWrap  = new Panel { Dock = DockStyle.Fill,  BackColor = Theme.InputBg, Padding = new Padding(10, 7, 6, 7) };
        var portWrap  = new Panel { Dock = DockStyle.Right, Width = 80, BackColor = Theme.InputBg, Padding = new Padding(6, 7, 10, 7) };
        _hostField.Dock = DockStyle.Fill;
        _portField.Dock = DockStyle.Fill;
        hostWrap.Controls.Add(_hostField);
        portWrap.Controls.Add(_portField);
        serverRow.Controls.Add(hostWrap);
        serverRow.Controls.Add(portWrap);

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
            Wrap(_newRoomNameField),
            Wrap(_roomCodeField),
            sepLabel,
            Wrap(_roomPasswordField),
            hint,
            Spacer(4),
            Wrap(_usernameField),
            Spacer(4),
            serverRow,
            Wrap(sub),
            Wrap(title),
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
        center.Height = Math.Min(Height - 60, 620);
        center.Left   = (Width  - center.Width)  / 2;
        center.Top    = (Height - center.Height) / 2;
    }

    private static void PaintRoundedPanel(object? sender, PaintEventArgs e)
    {
        if (sender is not Panel p) return;
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        var r = new Rectangle(0, 0, p.Width - 1, p.Height - 1);
        using var path = RoundRect(r, 14);
        using var fill = new SolidBrush(Theme.Surface);
        e.Graphics.FillPath(fill, path);
        using var pen = new Pen(Theme.Border, 1);
        e.Graphics.DrawPath(pen, path);
    }

    private static Panel Wrap(Control c)
    {
        var w = new Panel { Height = c is Label l ? l.Height : 38, BackColor = Color.Transparent };
        c.Dock = DockStyle.Fill;
        w.Controls.Add(c);
        return w;
    }

    private static Panel Wrap(TextBox tb)
    {
        var w = new Panel
        {
            Height    = 38,
            BackColor = Theme.InputBg,
            Padding   = new Padding(10, 7, 10, 7)
        };
        tb.Dock = DockStyle.Fill;
        w.Controls.Add(tb);
        return w;
    }

    private static Panel Spacer(int h) => new() { Height = h, BackColor = Color.Transparent };

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

    private static System.Drawing.Drawing2D.GraphicsPath RoundRect(Rectangle r, int radius)
    {
        var p = new System.Drawing.Drawing2D.GraphicsPath();
        p.AddArc(r.X,                  r.Y,                  radius * 2, radius * 2, 180, 90);
        p.AddArc(r.Right - radius * 2, r.Y,                  radius * 2, radius * 2, 270, 90);
        p.AddArc(r.Right - radius * 2, r.Bottom - radius * 2, radius * 2, radius * 2,  0, 90);
        p.AddArc(r.X,                  r.Bottom - radius * 2, radius * 2, radius * 2, 90, 90);
        p.CloseFigure();
        return p;
    }
}
