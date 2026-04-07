using Avalonia.Controls;
using TempChat.Services;

namespace TempChat.Views;

public partial class LoginView : UserControl
{
    public delegate void JoinCallback(string serverUrl, string roomCode, string username,
                                      string roomName, string roomPassword);

    private readonly JoinCallback _callback;

    public LoginView(string defaultHost, string defaultPort, JoinCallback callback)
    {
        _callback = callback;
        InitializeComponent();

        HostField.Text = defaultHost;
        PortField.Text = defaultPort;

        JoinBtn.Click   += async (_, _) => await HandleJoinAsync();
        CreateBtn.Click += async (_, _) => await HandleCreateAsync();
    }

    private async Task HandleJoinAsync()
    {
        string host     = HostField.Text?.Trim() ?? "";
        string port     = PortField.Text?.Trim() ?? "";
        string username = UsernameField.Text?.Trim() ?? "";
        string code     = RoomCodeField.Text?.Trim().ToUpper() ?? "";
        string password = PasswordField.Text ?? "";

        if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(port)) { SetStatus("Server IP and port are required."); return; }
        if (string.IsNullOrEmpty(username))                            { SetStatus("Username is required."); return; }
        if (string.IsNullOrEmpty(code))                                { SetStatus("Room code is required."); return; }

        string server = $"http://{host}:{port}";
        SetBusy(true, "Connecting…");
        try
        {
            var room = await new ApiClient(server).GetRoomAsync(code);
            SetBusy(false, " ");
            _callback(server, room.Code, username, room.Name, password);
        }
        catch (Exception ex) { SetBusy(false, $"Error: {Root(ex)}"); }
    }

    private async Task HandleCreateAsync()
    {
        string host     = HostField.Text?.Trim() ?? "";
        string port     = PortField.Text?.Trim() ?? "";
        string username = UsernameField.Text?.Trim() ?? "";
        string name     = NewRoomNameField.Text?.Trim() ?? "";
        string password = PasswordField.Text ?? "";

        if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(port)) { SetStatus("Server IP and port are required."); return; }
        if (string.IsNullOrEmpty(username))                            { SetStatus("Username is required."); return; }
        if (string.IsNullOrEmpty(name))                                { SetStatus("Room name is required."); return; }

        string server = $"http://{host}:{port}";
        SetBusy(true, "Creating room…");
        try
        {
            var room = await new ApiClient(server).CreateRoomAsync(name);
            SetBusy(false, " ");
            _callback(server, room.Code, username, room.Name, password);
        }
        catch (Exception ex) { SetBusy(false, $"Error: {Root(ex)}"); }
    }

    private void SetBusy(bool busy, string status)
    {
        JoinBtn.IsEnabled          = !busy;
        CreateBtn.IsEnabled        = !busy;
        HostField.IsEnabled        = !busy;
        PortField.IsEnabled        = !busy;
        UsernameField.IsEnabled    = !busy;
        PasswordField.IsEnabled    = !busy;
        RoomCodeField.IsEnabled    = !busy;
        NewRoomNameField.IsEnabled = !busy;
        StatusLabel.Text           = status;
    }

    private void SetStatus(string msg) => StatusLabel.Text = msg;

    private static string Root(Exception ex)
    {
        Exception e = ex;
        while (e.InnerException != null) e = e.InnerException;
        return e.Message ?? ex.GetType().Name;
    }
}
