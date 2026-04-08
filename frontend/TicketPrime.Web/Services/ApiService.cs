using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TicketPrime.Web.Models;

namespace TicketPrime.Web.Services;

public sealed class ApiService(HttpClient http) : IApiService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public async Task<IReadOnlyList<EventoDto>> GetEventosAsync(CancellationToken cancellationToken = default)
    {
        var response = await http.GetAsync("api/eventos", cancellationToken);
        response.EnsureSuccessStatusCode();
        var list = await response.Content.ReadFromJsonAsync<List<EventoDto>>(JsonOptions, cancellationToken);
        return list ?? [];
    }

    public async Task<ApiResult> PostEventoAsync(EventoCreateDto dto, CancellationToken cancellationToken = default)
    {
        var response = await http.PostAsJsonAsync("api/eventos", dto, JsonOptions, cancellationToken);
        return await MapResultAsync(response, cancellationToken);
    }

    public async Task<ApiResult> PostCupomAsync(CupomDto dto, CancellationToken cancellationToken = default)
    {
        var response = await http.PostAsJsonAsync("api/cupons", dto, JsonOptions, cancellationToken);
        return await MapResultAsync(response, cancellationToken);
    }

    public async Task<ApiResult> PostUsuarioAsync(UsuarioDto dto, CancellationToken cancellationToken = default)
    {
        var response = await http.PostAsJsonAsync("api/usuarios", dto, JsonOptions, cancellationToken);
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return ApiResult.Fail(string.IsNullOrWhiteSpace(body)
                ? "Não foi possível cadastrar o usuário."
                : body.Trim());
        }

        return await MapResultAsync(response, cancellationToken);
    }

    private static async Task<ApiResult> MapResultAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return ApiResult.Ok();

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var message = string.IsNullOrWhiteSpace(body)
            ? $"Erro {(int)response.StatusCode}: {response.ReasonPhrase}"
            : body.Trim();
        return ApiResult.Fail(message);
    }
}
