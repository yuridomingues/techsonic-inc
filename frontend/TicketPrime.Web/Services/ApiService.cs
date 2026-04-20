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

    public async Task<ApiResult> PostUsuarioAsync(UsuarioCreateDto dto, CancellationToken cancellationToken = default)
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

    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var response = await http.PostAsJsonAsync("api/auth/login", request, JsonOptions, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions, cancellationToken);
        }
        return null;
    }

    public async Task<IReadOnlyList<AssentoDto>> GetAssentosAsync(int eventoId, CancellationToken cancellationToken = default)
    {
        var response = await http.GetAsync($"api/eventos/{eventoId}/assentos", cancellationToken);
        response.EnsureSuccessStatusCode();
        var list = await response.Content.ReadFromJsonAsync<List<AssentoDto>>(JsonOptions, cancellationToken);
        return list ?? [];
    }

    public async Task<AssentoLockResponse?> LockAssentoAsync(AssentoLockRequest request, CancellationToken cancellationToken = default)
    {
        var response = await http.PostAsJsonAsync($"api/assentos/{request.SeatId}/lock", request, JsonOptions, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<AssentoLockResponse>(JsonOptions, cancellationToken);
        }
        return null;
    }

    public async Task<ApiResult> ReleaseAssentoAsync(int seatId, CancellationToken cancellationToken = default)
    {
        var response = await http.PostAsync($"api/assentos/{seatId}/release", null, cancellationToken);
        return await MapResultAsync(response, cancellationToken);
    }

    public async Task<FilaResponse?> JoinFilaAsync(FilaRequest request, CancellationToken cancellationToken = default)
    {
        var response = await http.PostAsJsonAsync("api/fila/join", request, JsonOptions, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<FilaResponse>(JsonOptions, cancellationToken);
        }
        return null;
    }

    public async Task<ApiResult> LeaveFilaAsync(int eventoId, CancellationToken cancellationToken = default)
    {
        var response = await http.PostAsync($"api/fila/leave?eventoId={eventoId}", null, cancellationToken);
        return await MapResultAsync(response, cancellationToken);
    }

    public async Task<FilaDto?> GetFilaPositionAsync(int eventoId, CancellationToken cancellationToken = default)
    {
        var response = await http.GetAsync($"api/fila/{eventoId}/position", cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<FilaDto>(JsonOptions, cancellationToken);
        }
        return null;
    }

    public async Task<ReservaDto?> CreateReservaAsync(ReservaCreateDto dto, CancellationToken cancellationToken = default)
    {
        var response = await http.PostAsJsonAsync("api/reservas", dto, JsonOptions, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<ReservaDto>(JsonOptions, cancellationToken);
        }
        return null;
    }

    public async Task<IReadOnlyList<ReservaDto>> GetReservasAsync(CancellationToken cancellationToken = default)
    {
        var response = await http.GetAsync("api/reservas", cancellationToken);
        response.EnsureSuccessStatusCode();
        var list = await response.Content.ReadFromJsonAsync<List<ReservaDto>>(JsonOptions, cancellationToken);
        return list ?? [];
    }

    public async Task<PagamentoDto?> SimulatePagamentoAsync(int reservaId, PagamentoCreateDto dto, CancellationToken cancellationToken = default)
    {
        var response = await http.PostAsJsonAsync($"api/reservas/{reservaId}/pagamentos", dto, JsonOptions, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<PagamentoDto>(JsonOptions, cancellationToken);
        }
        return null;
    }

    public async Task<ApiResult> CancelReservaAsync(int reservaId, CancellationToken cancellationToken = default)
    {
        var response = await http.PostAsync($"api/reservas/{reservaId}/cancelar", null, cancellationToken);
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
