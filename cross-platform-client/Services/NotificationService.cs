using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Threading;

namespace TempChat.Services;

/// <summary>
/// Cross-platform in-app toast notifications via Avalonia's WindowNotificationManager.
/// Shows a toast in the corner of the main window whenever a message arrives while
/// the window is not focused.  No OS-level tray icon is required.
/// </summary>
public static class NotificationService
{
    private static WindowNotificationManager? _manager;
    private static Window?                    _window;

    public static void Init(Window window)
    {
        _window  = window;
        _manager = new WindowNotificationManager(window)
        {
            Position = NotificationPosition.BottomRight,
            MaxItems = 3
        };
    }

    public static void NotifyIfUnfocused(string sender, string message)
    {
        if (_manager == null || _window == null) return;
        if (_window.IsActive) return;

        Dispatcher.UIThread.Post(() =>
            _manager.Show(new Notification(
                $"TempChat — {sender}",
                message,
                NotificationType.Information,
                TimeSpan.FromSeconds(4))));
    }
}
