using System.Net.Http.Headers;

namespace TicketPrime.Web.Services;

public sealed class AuthHttpMessageHandler(AuthSessionStore authSessionStore) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var session = await authSessionStore.GetSessionAsync();
        if (!string.IsNullOrWhiteSpace(session?.Token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.Token);

        return await base.SendAsync(request, cancellationToken);
    }
}
