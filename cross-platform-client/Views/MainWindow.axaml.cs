using Avalonia.Controls;
using TempChat.Services;

namespace TempChat.Views;

public partial class MainWindow : Window
{
    private const string DefaultHost = "localhost";
    private const string DefaultPort = "8080";

    private ChatView? _chatView;

    public MainWindow()
    {
        InitializeComponent();
        NotificationService.Init(this);
        ShowLogin();
    }

    private void ShowLogin()
    {
        var login = new LoginView(DefaultHost, DefaultPort, OnJoin);
        MainContent.Content = login;
        Title = "TempChat";
    }

    private void OnJoin(string serverUrl, string roomCode, string username,
                        string roomName, string roomPassword)
    {
        _chatView?.Dispose();
        _chatView = new ChatView(serverUrl, roomCode, username, roomName, roomPassword, OnLeave);
        MainContent.Content = _chatView;
        Title = $"TempChat — {roomName} [{roomCode}]";
    }

    private void OnLeave()
    {
        _chatView?.Dispose();
        _chatView = null;
        ShowLogin();
    }
}
