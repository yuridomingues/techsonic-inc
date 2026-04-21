using Microsoft.JSInterop;

namespace TicketPrime.Web.Services;

public sealed class BrowserStorageService(IJSRuntime jsRuntime)
{
    public ValueTask<string?> GetItemAsync(string key) => jsRuntime.InvokeAsync<string?>("ticketPrimeAuth.get", key);

    public ValueTask SetItemAsync(string key, string value) => jsRuntime.InvokeVoidAsync("ticketPrimeAuth.set", key, value);

    public ValueTask RemoveItemAsync(string key) => jsRuntime.InvokeVoidAsync("ticketPrimeAuth.remove", key);
}
