using System.Text.Json;
using Microsoft.JSInterop;

namespace MeshBoard.Client.Channels;

public sealed class ChannelPreferencesStore
{
    private const string StorageKeyPrefix = "meshboard:channel:";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IJSRuntime _jsRuntime;

    public ChannelPreferencesStore(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task<ChannelPreference> GetAsync(string channelKey)
    {
        if (string.IsNullOrWhiteSpace(channelKey))
        {
            return new ChannelPreference();
        }

        var json = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", StorageKey(channelKey));

        if (string.IsNullOrWhiteSpace(json))
        {
            return new ChannelPreference();
        }

        try
        {
            return JsonSerializer.Deserialize<ChannelPreference>(json, JsonOptions) ?? new ChannelPreference();
        }
        catch
        {
            return new ChannelPreference();
        }
    }

    public async Task SetLabelAsync(string channelKey, string label)
    {
        var pref = await GetAsync(channelKey);
        pref.Label = label.Trim();
        await SaveAsync(channelKey, pref);
    }

    public async Task SetFavoriteAsync(string channelKey, bool isFavorite)
    {
        var pref = await GetAsync(channelKey);
        pref.IsFavorite = isFavorite;
        await SaveAsync(channelKey, pref);
    }

    private async Task SaveAsync(string channelKey, ChannelPreference preference)
    {
        var json = JsonSerializer.Serialize(preference, JsonOptions);
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", StorageKey(channelKey), json);
    }

    private static string StorageKey(string channelKey)
    {
        return $"{StorageKeyPrefix}{channelKey}";
    }
}
