using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using TicketPrime.Web.Models;

namespace TicketPrime.Web.Services;

public sealed class AppAuthenticationStateProvider(AuthSessionStore authSessionStore) : AuthenticationStateProvider
{
    private static readonly ClaimsPrincipal Anonymous = new(new ClaimsIdentity());

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var session = await authSessionStore.GetSessionAsync();
        return new AuthenticationState(CreatePrincipal(session));
    }

    public async Task SignInAsync(LoginResponse session)
    {
        await authSessionStore.SetSessionAsync(session);
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public async Task SignOutAsync()
    {
        await authSessionStore.ClearSessionAsync();
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(Anonymous)));
    }

    private static ClaimsPrincipal CreatePrincipal(LoginResponse? session)
    {
        if (session is null || session.ExpiresAt <= DateTime.UtcNow)
            return Anonymous;

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, session.Cpf),
            new(ClaimTypes.Name, session.Nome),
            new(ClaimTypes.Email, session.Email),
        };

        if (session.IsAdmin)
            claims.Add(new Claim(ClaimTypes.Role, "Admin"));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "ticketprime.auth"));
    }
}
