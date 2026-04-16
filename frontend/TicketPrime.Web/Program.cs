using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using TicketPrime.Web;
using TicketPrime.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBase = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5000/";
if (!apiBase.EndsWith('/'))
    apiBase += "/";

builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(apiBase) });
builder.Services.AddScoped<IApiService, ApiService>();
builder.Services.AddMudServices();

await builder.Build().RunAsync();
