using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using TicketPrime.Web;
using TicketPrime.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<BrowserStorageService>();
builder.Services.AddScoped<AuthSessionStore>();
builder.Services.AddScoped<AppAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<AppAuthenticationStateProvider>());
builder.Services.AddTransient<AuthHttpMessageHandler>();
builder.Services.AddScoped(sp =>
{
	var handler = sp.GetRequiredService<AuthHttpMessageHandler>();
	handler.InnerHandler = new HttpClientHandler();
	return new HttpClient(handler) { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) };
});
builder.Services.AddScoped<IApiService, ApiService>();
builder.Services.AddMudServices();

await builder.Build().RunAsync();
