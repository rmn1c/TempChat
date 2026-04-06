using TempChat.Services;

namespace TempChat.UI;

public sealed class LoginPanel : Panel
{
    public delegate void JoinCallback(string serverUrl, string roomCode, string username,
                                      string roomName, string roomPassword);

    private readonly TextBox _hostField;
    private readonly TextBox _portField;
    private readonly TextBox _usernameField;
    private readonly TextBox _roomPasswordField;
    private readonly TextBox _roomCodeField;
    private readonly TextBox _newRoomNameField;
    private readonly Button  _joinBtn;
    private readonly Button  _createBtn;
    private readonly Label   _statusLabel;
    private readonly JoinCallback _callback;

    public LoginPanel(string defaultHost, string defaultPort, JoinCallback callback)
    {
        _callback = callback;
        Padding   = new Padding(30, 20, 30, 20);

        var layout = new TableLayoutPanel
        {
            Dock        = DockStyle.Fill,
            ColumnCount = 2,
            RowCount    = 11,
            AutoSize    = true
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,  100));

        // Title
        var title = new Label
        {
            Text      = "TempChat",
            Font      = new Font("Segoe UI", 22, FontStyle.Bold),
            Dock      = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter
        };
        layout.Controls.Add(title, 0, 0);
        layout.SetColumnSpan(title, 2);

        _hostField         = new TextBox { Text = defaultHost,  Dock = DockStyle.Fill };
        _portField         = new TextBox { Text = defaultPort,  Dock = DockStyle.Fill };
        _usernameField     = new TextBox { Dock = DockStyle.Fill };
        _roomPasswordField = new TextBox { Dock = DockStyle.Fill, UseSystemPasswordChar = true };
        _roomCodeField     = new TextBox { Dock = DockStyle.Fill };
        _newRoomNameField  = new TextBox { Dock = DockStyle.Fill };

        AddRow(layout, 1, "Server IP:",     _hostField);
        AddRow(layout, 2, "Port:",          _portField);
        AddRow(layout, 3, "Username:",      _usernameField);
        AddRow(layout, 4, "Room Password:", _roomPasswordField);

        var sep = new Label
        {
            BorderStyle = BorderStyle.Fixed3D,
            Height      = 2,
            Dock        = DockStyle.Fill,
            Margin      = new Padding(0, 8, 0, 8)
        };
        layout.Controls.Add(sep, 0, 5);
        layout.SetColumnSpan(sep, 2);

        AddRow(layout, 6, "Room Code:",     _roomCodeField);
        AddRow(layout, 7, "New Room Name:", _newRoomNameField);

        var hint = new Label
        {
            Text      = "Room Password is used for end-to-end encryption.\nAll members must use the same password.",
            ForeColor = Color.Gray,
            Font      = new Font("Segoe UI", 8.5f, FontStyle.Italic),
            Dock      = DockStyle.Fill,
            AutoSize  = false,
            Height    = 36
        };
        layout.Controls.Add(hint, 0, 8);
        layout.SetColumnSpan(hint, 2);

        _joinBtn   = new Button { Text = "Join Room",   Dock = DockStyle.Fill, Height = 32 };
        _createBtn = new Button { Text = "Create Room", Dock = DockStyle.Fill, Height = 32 };
        _statusLabel = new Label
        {
            Text      = " ",
            ForeColor = Color.Red,
            Dock      = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter
        };

        layout.Controls.Add(_joinBtn,     0, 9);
        layout.SetColumnSpan(_joinBtn, 2);
        layout.Controls.Add(_createBtn,   0, 10);
        layout.SetColumnSpan(_createBtn, 2);

        var statusRow = new Label
        {
            Text      = " ",
            ForeColor = Color.Red,
            Dock      = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter
        };
        _statusLabel = statusRow;
        layout.RowCount = 12;
        layout.Controls.Add(_statusLabel, 0, 11);
        layout.SetColumnSpan(_statusLabel, 2);

        Controls.Add(layout);

        _joinBtn.Click   += async (_, _) => await HandleJoinAsync();
        _createBtn.Click += async (_, _) => await HandleCreateAsync();
    }

    private static void AddRow(TableLayoutPanel layout, int row, string labelText, Control field)
    {
        layout.Controls.Add(new Label
        {
            Text      = labelText,
            Dock      = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            Padding   = new Padding(0, 0, 6, 0)
        }, 0, row);
        layout.Controls.Add(field, 1, row);
    }

    private async Task HandleJoinAsync()
    {
        string host     = _hostField.Text.Trim();
        string port     = _portField.Text.Trim();
        string username = _usernameField.Text.Trim();
        string code     = _roomCodeField.Text.Trim().ToUpper();

        if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(port)) { SetStatus("Server IP and port are required."); return; }
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(code)) { SetStatus("Username and room code are required."); return; }

        string server   = $"http://{host}:{port}";
        string password = _roomPasswordField.Text;
        SetBusy(true, "Connecting...");

        try
        {
            var room = await new ApiClient(server).GetRoomAsync(code);
            SetBusy(false, " ");
            _callback(server, room.Code, username, room.Name, password);
        }
        catch (Exception ex)
        {
            SetBusy(false, $"Error: {RootMessage(ex)}");
        }
    }

    private async Task HandleCreateAsync()
    {
        string host     = _hostField.Text.Trim();
        string port     = _portField.Text.Trim();
        string username = _usernameField.Text.Trim();
        string name     = _newRoomNameField.Text.Trim();

        if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(port)) { SetStatus("Server IP and port are required."); return; }
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(name)) { SetStatus("Username and room name are required."); return; }

        string server   = $"http://{host}:{port}";
        string password = _roomPasswordField.Text;
        SetBusy(true, "Creating room...");

        try
        {
            var room = await new ApiClient(server).CreateRoomAsync(name);
            SetBusy(false, " ");
            _callback(server, room.Code, username, room.Name, password);
        }
        catch (Exception ex)
        {
            SetBusy(false, $"Error: {RootMessage(ex)}");
        }
    }

    private void SetBusy(bool busy, string status)
    {
        _joinBtn.Enabled          = !busy;
        _createBtn.Enabled        = !busy;
        _hostField.Enabled        = !busy;
        _portField.Enabled        = !busy;
        _usernameField.Enabled    = !busy;
        _roomPasswordField.Enabled = !busy;
        _roomCodeField.Enabled    = !busy;
        _newRoomNameField.Enabled = !busy;
        SetStatus(status);
    }

    private void SetStatus(string msg) => _statusLabel.Text = msg;

    private static string RootMessage(Exception ex)
    {
        Exception e = ex;
        while (e.InnerException != null) e = e.InnerException;
        return e.Message ?? ex.GetType().Name;
    }
}
