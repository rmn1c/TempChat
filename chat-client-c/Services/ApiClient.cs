using System.Net.Http.Json;
using System.Text.Json;

namespace TempChat.Services;

public sealed class ApiClient
{
    public record RoomInfo(string Code, string Name, string ExpiresAt);
    public record ChatMessage(string Sender, string Content, string SentAt);

    private static readonly HttpClient Http = new();
    private readonly string _baseUrl;

    public ApiClient(string baseUrl)
    {
        _baseUrl = baseUrl.TrimEnd('/');
    }

    public async Task<RoomInfo> CreateRoomAsync(string name)
    {
        var resp = await Http.PostAsJsonAsync($"{_baseUrl}/api/rooms", new { name });
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return ParseRoom(json);
    }

    public async Task<RoomInfo> GetRoomAsync(string code)
    {
        var resp = await Http.GetAsync($"{_baseUrl}/api/rooms/{code}");
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return ParseRoom(json);
    }

    public async Task<List<ChatMessage>> GetMessagesAsync(string roomCode)
    {
        var resp = await Http.GetAsync($"{_baseUrl}/api/rooms/{roomCode}/messages");
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var result = new List<ChatMessage>();
        foreach (var item in json.EnumerateArray())
            result.Add(new ChatMessage(
                item.GetProperty("sender").GetString()!,
                item.GetProperty("content").GetString()!,
                item.GetProperty("sentAt").GetString()!));
        return result;
    }

    private static RoomInfo ParseRoom(JsonElement json) =>
        new(json.GetProperty("code").GetString()!,
            json.GetProperty("name").GetString()!,
            json.GetProperty("expiresAt").GetString()!);
}
