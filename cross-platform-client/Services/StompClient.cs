using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace TempChat.Services;

/// <summary>
/// Minimal STOMP-over-WebSocket client.
/// Connects to ws://host:port/ws/websocket (SockJS raw endpoint).
///
/// onMessage receives (sender, content) so the caller can decrypt before display.
/// </summary>
public sealed class StompClient : IDisposable
{
    private readonly ClientWebSocket _ws = new();
    private readonly Uri _wsUri;
    private readonly string _roomCode;
    private readonly string _username;
    private readonly Action<string, string> _onMessage;
    private readonly Action<string> _onUserEvent;
    private readonly CancellationTokenSource _cts = new();
    private int _subId;

    public StompClient(string wsUrl, string roomCode, string username,
        Action<string, string> onMessage, Action<string> onUserEvent)
    {
        _wsUri       = new Uri(wsUrl);
        _roomCode    = roomCode;
        _username    = username;
        _onMessage   = onMessage;
        _onUserEvent = onUserEvent;
    }

    public async Task ConnectAsync()
    {
        await _ws.ConnectAsync(_wsUri, _cts.Token);
        await SendRawAsync("CONNECT\naccept-version:1.2\nheart-beat:0,0\n\n\0");
        _ = ReadLoopAsync();
    }

    public async Task SendChatMessageAsync(string content)
    {
        string json = JsonSerializer.Serialize(new
        {
            sender   = _username,
            content,
            roomCode = _roomCode
        });
        await SendStompAsync($"/app/chat/{_roomCode}", json);
    }

    public async Task LeaveRoomAsync()
    {
        try { await SendStompAsync($"/app/leave/{_roomCode}", $"\"{_username}\""); }
        catch { /* best-effort */ }
    }

    // ── internals ─────────────────────────────────────────────────────

    private async Task SendStompAsync(string destination, string body)
    {
        string frame = $"SEND\ndestination:{destination}\ncontent-type:application/json\n\n{body}\0";
        await SendRawAsync(frame);
    }

    private async Task SendRawAsync(string frame)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(frame);
        await _ws.SendAsync(bytes, WebSocketMessageType.Text, true, _cts.Token);
    }

    private async Task ReadLoopAsync()
    {
        var buffer = new byte[8192];
        var sb     = new StringBuilder();

        try
        {
            while (_ws.State == WebSocketState.Open && !_cts.IsCancellationRequested)
            {
                WebSocketReceiveResult result;
                do
                {
                    result = await _ws.ReceiveAsync(buffer, _cts.Token);
                    if (result.MessageType == WebSocketMessageType.Close) return;
                    sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                }
                while (!result.EndOfMessage);

                foreach (string frame in sb.ToString().Split('\0'))
                    if (!string.IsNullOrWhiteSpace(frame))
                        ProcessFrame(frame.Trim('\r', '\n', ' '));

                sb.Clear();
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception) { }
    }

    private void ProcessFrame(string frame)
    {
        if (frame.StartsWith("CONNECTED"))
        {
            _ = Task.Run(async () =>
            {
                await SubscribeAsync($"/topic/chat/{_roomCode}");
                await SubscribeAsync($"/topic/users/{_roomCode}");
                await SendStompAsync($"/app/join/{_roomCode}", $"\"{_username}\"");
            });
            return;
        }

        if (!frame.StartsWith("MESSAGE")) return;

        string body = ExtractBody(frame);
        string dest = ExtractHeader(frame, "destination") ?? "";

        if (dest.StartsWith("/topic/users/"))
        {
            _onUserEvent(body.Trim('"', '\r', '\n'));
            return;
        }

        try
        {
            using var doc  = JsonDocument.Parse(body);
            var root       = doc.RootElement;
            string sender  = root.TryGetProperty("sender",  out var s) ? s.GetString() ?? "?" : "?";
            string content = root.TryGetProperty("content", out var c) ? c.GetString() ?? body : body;
            _onMessage(sender, content);
        }
        catch
        {
            _onMessage("?", body);
        }
    }

    private async Task SubscribeAsync(string topic)
    {
        int id = Interlocked.Increment(ref _subId);
        await SendRawAsync($"SUBSCRIBE\nid:sub-{id}\ndestination:{topic}\n\n\0");
    }

    private static string ExtractBody(string frame)
    {
        int idx = frame.IndexOf("\n\n", StringComparison.Ordinal);
        return idx < 0 ? frame : frame[(idx + 2)..];
    }

    private static string? ExtractHeader(string frame, string name)
    {
        foreach (string line in frame.Split('\n'))
            if (line.StartsWith(name + ":"))
                return line[(name.Length + 1)..].Trim();
        return null;
    }

    public void Dispose()
    {
        _cts.Cancel();
        _ws.Dispose();
        _cts.Dispose();
    }
}
