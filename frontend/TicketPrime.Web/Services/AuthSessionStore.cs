using System.Text.Json;
using TicketPrime.Web.Models;

namespace TicketPrime.Web.Services;

public sealed class AuthSessionStore(BrowserStorageService storage)
{
    private const string StorageKey = "ticketprime.auth.session";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private LoginResponse? _cachedSession;
    private bool _isLoaded;

    public async Task<LoginResponse?> GetSessionAsync()
    {
        if (!_isLoaded)
            await LoadAsync();

        if (_cachedSession is not null && _cachedSession.ExpiresAt <= DateTime.UtcNow)
        {
            await ClearSessionAsync();
            return null;
        }

        return _cachedSession;
    }

    public async Task SetSessionAsync(LoginResponse session)
    {
        _cachedSession = session;
        _isLoaded = true;

        var serialized = JsonSerializer.Serialize(session, JsonOptions);
        await storage.SetItemAsync(StorageKey, serialized);
    }

    public async Task ClearSessionAsync()
    {
        _cachedSession = null;
        _isLoaded = true;
        await storage.RemoveItemAsync(StorageKey);
    }

    private async Task LoadAsync()
    {
        _isLoaded = true;

        var rawSession = await storage.GetItemAsync(StorageKey);
        if (string.IsNullOrWhiteSpace(rawSession))
        {
            _cachedSession = null;
            return;
        }

        try
        {
            _cachedSession = JsonSerializer.Deserialize<LoginResponse>(rawSession, JsonOptions);
        }
        catch (JsonException)
        {
            _cachedSession = null;
            await storage.RemoveItemAsync(StorageKey);
        }
    }
}
