using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using TempChat.Services;

namespace TempChat.Views;

public partial class ChatView : UserControl, IDisposable
{
    private record Msg(string Sender, string Content, string Time, bool IsOwn);

    private readonly string         _serverUrl;
    private readonly string         _roomCode;
    private readonly string         _username;
    private readonly Action         _onLeave;
    private readonly CryptoService? _crypto;

    private readonly List<Msg>         _messages            = new();
    private readonly HashSet<string>   _historyFingerprints = new();
    private StompClient? _stomp;

    // ── Shared brushes ────────────────────────────────────────────────
    private static readonly ISolidColorBrush OwnBubbleBrush   = new SolidColorBrush(Color.Parse("#413078"));
    private static readonly ISolidColorBrush OtherBubbleBrush = new SolidColorBrush(Color.Parse("#182533"));
    private static readonly ISolidColorBrush TextBrush        = new SolidColorBrush(Color.Parse("#e6ebf0"));
    private static readonly ISolidColorBrush SubTextBrush     = new SolidColorBrush(Color.Parse("#9aa7b2"));
    private static readonly ISolidColorBrush PurpleBrush      = new SolidColorBrush(Color.Parse("#a86ef0"));
    private static readonly ISolidColorBrush AccentBrush      = new SolidColorBrush(Color.Parse("#2ea6ff"));

    public ChatView(string serverUrl, string roomCode, string username,
                    string roomName, string roomPassword, Action onLeave)
    {
        _serverUrl = serverUrl;
        _roomCode  = roomCode;
        _username  = username;
        _onLeave   = onLeave;

        if (!string.IsNullOrEmpty(roomPassword))
            try { _crypto = new CryptoService(roomPassword, roomCode); } catch { }

        InitializeComponent();

        string encBadge = _crypto != null ? " 🔒" : "";
        RoomNameLabel.Text = roomName + encBadge;

        // Code label — click to copy
        string origText = $"Code: {roomCode}  ·  {username}";
        CodeLabel.Text = origText;
        CodeLabel.Tapped += async (_, _) =>
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard != null)
                await clipboard.SetTextAsync(roomCode);
            CodeLabel.Text       = "✓  Code copied!";
            CodeLabel.Foreground = AccentBrush;
            await Task.Delay(1800);
            CodeLabel.Text       = origText;
            CodeLabel.Foreground = SubTextBrush;
        };

        LeaveBtn.Click += async (_, _) => await LeaveAsync();
        SendBtn.Click  += async (_, _) => await SendAsync();

        InputField.KeyDown += async (_, e) =>
        {
            if (e.Key == Key.Enter) { e.Handled = true; await SendAsync(); }
        };

        ScrollDownBtn.Click += (_, _) => ScrollToBottom();

        // Show/hide scroll-down button when the user scrolls
        MessageScroll.PropertyChanged += (_, e) =>
        {
            if (e.Property == ScrollViewer.OffsetProperty)
                UpdateScrollDownButton();
        };

        _ = LoadAndConnectAsync();
    }

    // ── Scrolling ─────────────────────────────────────────────────────

    private bool IsAtBottom()
    {
        double max = MessageScroll.Extent.Height - MessageScroll.Viewport.Height;
        return max <= 0 || MessageScroll.Offset.Y >= max - 80;
    }

    private void ScrollToBottom()
    {
        Dispatcher.UIThread.Post(() =>
        {
            MessageScroll.Offset = new Vector(0, double.MaxValue);
            UpdateScrollDownButton();
        }, DispatcherPriority.Loaded);
    }

    private void UpdateScrollDownButton()
    {
        double max = MessageScroll.Extent.Height - MessageScroll.Viewport.Height;
        ScrollDownBtn.IsVisible = max > 50 && !IsAtBottom();
    }

    // ── Messages ──────────────────────────────────────────────────────

    private void AddMessage(string sender, string content, string time,
                            bool isOwn, bool animate)
    {
        bool wasAtBottom = IsAtBottom();

        _messages.Add(new Msg(sender, content, time, isOwn));
        var bubble = MakeBubble(sender, content, time, isOwn);

        if (animate)
        {
            bubble.Opacity = 0;
            bubble.Transitions = new Transitions
            {
                new DoubleTransition
                {
                    Property = OpacityProperty,
                    Duration = TimeSpan.FromMilliseconds(150)
                }
            };
            // Let the layout pass complete, then fade in
            Dispatcher.UIThread.Post(() => bubble.Opacity = 1, DispatcherPriority.Loaded);
        }

        MessageStack.Children.Add(bubble);

        if (isOwn || wasAtBottom)
            ScrollToBottom();
        else
            UpdateScrollDownButton();
    }

    private static Control MakeBubble(string sender, string content, string time, bool isOwn)
    {
        var innerStack = new StackPanel { Spacing = 2 };

        if (!isOwn)
        {
            innerStack.Children.Add(new TextBlock
            {
                Text       = sender,
                FontSize   = 8.5,
                FontWeight = FontWeight.Bold,
                Foreground = PurpleBrush
            });
        }

        innerStack.Children.Add(new TextBlock
        {
            Text         = content,
            FontSize     = 10.5,
            Foreground   = TextBrush,
            TextWrapping = TextWrapping.Wrap
        });

        innerStack.Children.Add(new TextBlock
        {
            Text                = time,
            FontSize            = 8,
            Foreground          = SubTextBrush,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin              = new Thickness(0, 2, 0, 0)
        });

        return new Border
        {
            Background          = isOwn ? OwnBubbleBrush : OtherBubbleBrush,
            CornerRadius        = new CornerRadius(14),
            Padding             = new Thickness(14, 8),
            MaxWidth            = 520,
            Child               = innerStack,
            HorizontalAlignment = isOwn ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            Margin              = isOwn
                ? new Thickness(60, 2, 8, 2)
                : new Thickness(8, 2, 60, 2)
        };
    }

    private void AddSystemLine(string text)
    {
        var lbl = new TextBlock
        {
            Text                = $"— {text} —",
            FontSize            = 9,
            Foreground          = SubTextBrush,
            TextAlignment       = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin              = new Thickness(0, 6, 0, 6)
        };
        MessageStack.Children.Add(lbl);
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
                    if (_historyFingerprints.Remove(MakeFingerprint(sender, content))) return;

                    string plain = Decrypt(content);
                    string t     = DateTime.Now.ToString("HH:mm");
                    AddMessage(sender, plain, t, sender == _username, true);

                    if (sender != _username)
                        NotificationService.NotifyIfUnfocused(sender, plain);
                }),
                onUserEvent: evt => SafeInvoke(() => AddSystemLine(evt)));

            await _stomp.ConnectAsync();
        }
        catch (Exception ex) { SafeInvoke(() => AddSystemLine($"WebSocket error: {ex.Message}")); }
    }

    private async Task SendAsync()
    {
        string text = InputField.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(text) || _stomp == null) return;
        InputField.Text = "";
        try
        {
            string payload = _crypto != null ? _crypto.Encrypt(text) : text;
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

    private void SafeInvoke(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
            action();
        else
            Dispatcher.UIThread.Post(action);
    }

    public void Dispose()
    {
        _stomp?.Dispose();
        _stomp = null;
    }
}
