namespace TempChat.Services;

/// <summary>
/// Shows Windows balloon notifications via NotifyIcon.
/// Call Init() once from the main form. Call Show() from any thread.
/// </summary>
public static class ToastService
{
    private static NotifyIcon? _icon;
    private static Form?       _owner;

    public static void Init(Form owner)
    {
        _owner = owner;
        _icon  = new NotifyIcon
        {
            Icon    = SystemIcons.Application,
            Visible = true,
            Text    = "TempChat"
        };

        // Clicking the balloon or tray icon brings the window to front
        _icon.BalloonTipClicked += (_, _) => BringToFront();
        _icon.Click             += (_, _) => BringToFront();

        owner.FormClosed += (_, _) =>
        {
            _icon.Visible = false;
            _icon.Dispose();
            _icon = null;
        };
    }

    /// <summary>Show a notification if the main window is not focused.</summary>
    public static void NotifyIfUnfocused(string sender, string message)
    {
        if (_icon == null) return;
        if (_owner != null && _owner.ContainsFocus) return;

        // Must run on UI thread
        if (_owner?.InvokeRequired == true)
            _owner.Invoke(() => NotifyIfUnfocused(sender, message));
        else
            _icon.ShowBalloonTip(4000, $"TempChat — {sender}", message, ToolTipIcon.None);
    }

    private static void BringToFront()
    {
        if (_owner == null) return;
        _owner.Show();
        _owner.WindowState = FormWindowState.Normal;
        _owner.Activate();
    }
}
