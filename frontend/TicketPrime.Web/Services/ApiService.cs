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

    public async Task<ApiResult<EventoDto>> PostEventoAsync(EventoCreateDto dto, CancellationToken cancellationToken = default)
    {
        var response = await http.PostAsJsonAsync("api/eventos", dto, JsonOptions, cancellationToken);
        return await MapDataEnvelopeAsync<EventoDto>(response, cancellationToken);
    }

    public async Task<ApiResult> PostCupomAsync(CupomDto dto, CancellationToken cancellationToken = default)
    {
        var response = await http.PostAsJsonAsync("api/cupons", dto, JsonOptions, cancellationToken);
        return await MapOperationEnvelopeAsync(response, cancellationToken);
    }

    public async Task<ApiResult<UsuarioCadastroResponse>> PostUsuarioAsync(UsuarioCreateDto dto, CancellationToken cancellationToken = default)
    {
        var response = await http.PostAsJsonAsync("api/usuarios", dto, JsonOptions, cancellationToken);
        return await MapDataEnvelopeAsync<UsuarioCadastroResponse>(response, cancellationToken);
    }

    public async Task<ApiResult<UsuarioEmailVerificationResponse>> VerifyUserEmailAsync(UsuarioEmailVerificationRequest dto, CancellationToken cancellationToken = default)
    {
        var response = await http.PostAsJsonAsync("api/usuarios/verificar-email", dto, JsonOptions, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            var payload = await response.Content.ReadFromJsonAsync<UsuarioEmailVerificationResponse>(JsonOptions, cancellationToken);
            if (payload is null)
                return ApiResult<UsuarioEmailVerificationResponse>.Fail("Resposta inválida ao validar e-mail.");

            return payload.Success
                ? ApiResult<UsuarioEmailVerificationResponse>.Ok(payload)
                : ApiResult<UsuarioEmailVerificationResponse>.Fail(payload.Message);
        }

        return await MapResultAsync<UsuarioEmailVerificationResponse>(response, cancellationToken);
    }

    public async Task<ApiResult<EmailVerificationDeliveryResponse>> ResendUserVerificationCodeAsync(UsuarioCodigoReenvioRequest dto, CancellationToken cancellationToken = default)
    {
        var response = await http.PostAsJsonAsync("api/usuarios/reenviar-codigo", dto, JsonOptions, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            var payload = await response.Content.ReadFromJsonAsync<EmailVerificationDeliveryResponse>(JsonOptions, cancellationToken);
            if (payload is null)
                return ApiResult<EmailVerificationDeliveryResponse>.Fail("Resposta inválida ao reenviar código.");

            return payload.Success
                ? ApiResult<EmailVerificationDeliveryResponse>.Ok(payload)
                : ApiResult<EmailVerificationDeliveryResponse>.Fail(payload.Message);
        }

        return await MapResultAsync<EmailVerificationDeliveryResponse>(response, cancellationToken);
    }

    public async Task<ApiResult<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var response = await http.PostAsJsonAsync("api/auth/login", request, JsonOptions, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            var payload = await response.Content.ReadFromJsonAsync<LoginAttemptResponse>(JsonOptions, cancellationToken);
            if (payload is null)
                return ApiResult<LoginResponse>.Fail("Resposta inválida ao entrar.");

            return payload.Success && payload.Session is not null
                ? ApiResult<LoginResponse>.Ok(payload.Session)
                : ApiResult<LoginResponse>.Fail(payload.Message);
        }

        return await MapResultAsync<LoginResponse>(response, cancellationToken);
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
        return await MapOperationEnvelopeAsync(response, cancellationToken);
    }

    public async Task<ApiResult<FilaResponse>> JoinFilaAsync(FilaRequest request, CancellationToken cancellationToken = default)
    {
        var response = await http.PostAsJsonAsync("api/fila/join", request, JsonOptions, cancellationToken);
        return await MapDataEnvelopeAsync<FilaResponse>(response, cancellationToken);
    }

    public async Task<ApiResult> LeaveFilaAsync(int eventoId, CancellationToken cancellationToken = default)
    {
        var response = await http.PostAsJsonAsync("api/fila/leave", new FilaRequest(eventoId), JsonOptions, cancellationToken);
        return await MapOperationEnvelopeAsync(response, cancellationToken);
    }

    public async Task<FilaDto?> GetFilaPositionAsync(int eventoId, CancellationToken cancellationToken = default)
    {
        var response = await http.GetAsync($"api/fila/{eventoId}/position", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return null;
        }

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<FilaDto>(JsonOptions, cancellationToken);
        }
        return null;
    }

    public async Task<ApiResult<ReservaDto>> CreateReservaAsync(ReservaCreateDto dto, CancellationToken cancellationToken = default)
    {
        var response = await http.PostAsJsonAsync("api/reservas", dto, JsonOptions, cancellationToken);
        return await MapDataEnvelopeAsync<ReservaDto>(response, cancellationToken);
    }

    public async Task<IReadOnlyList<ReservaDto>> GetReservasAsync(CancellationToken cancellationToken = default)
    {
        var response = await http.GetAsync("api/reservas/me", cancellationToken);
        response.EnsureSuccessStatusCode();
        var list = await response.Content.ReadFromJsonAsync<List<ReservaDto>>(JsonOptions, cancellationToken);
        return list ?? [];
    }

    public async Task<ApiResult<PagamentoDto>> SimulatePagamentoAsync(int reservaId, PagamentoCreateDto dto, CancellationToken cancellationToken = default)
    {
        var response = await http.PostAsJsonAsync($"api/reservas/{reservaId}/pagamentos", dto, JsonOptions, cancellationToken);
        return await MapDataEnvelopeAsync<PagamentoDto>(response, cancellationToken);
    }

    public async Task<ApiResult> CancelReservaAsync(int reservaId, CancellationToken cancellationToken = default)
    {
        var response = await http.PostAsync($"api/reservas/{reservaId}/cancelar", null, cancellationToken);
        return await MapOperationEnvelopeAsync(response, cancellationToken);
    }

    public async Task<AdminMetricas> GetAdminMetricasAsync(CancellationToken cancellationToken = default)
    {
        var response = await http.GetAsync("api/admin/metricas", cancellationToken);
        response.EnsureSuccessStatusCode();
        var metricas = await response.Content.ReadFromJsonAsync<AdminMetricas>(JsonOptions, cancellationToken);
        return metricas ?? new AdminMetricas(0, 0, 0, 0);
    }

    public async Task<IReadOnlyList<SetorDto>> GetSetoresAsync(int eventoId, CancellationToken cancellationToken = default)
    {
        var response = await http.GetAsync($"api/eventos/{eventoId}/setores", cancellationToken);
        response.EnsureSuccessStatusCode();
        var list = await response.Content.ReadFromJsonAsync<List<SetorDto>>(JsonOptions, cancellationToken);
        return list ?? [];
    }

    public async Task<ApiResult> PostSetorAsync(int eventoId, SetorCreateDto dto, CancellationToken cancellationToken = default)
    {
        var response = await http.PostAsJsonAsync($"api/eventos/{eventoId}/setores", dto, JsonOptions, cancellationToken);
        return await MapOperationEnvelopeAsync(response, cancellationToken);
    }

    public async Task<ApiResult> GerarSetoresAsync(int eventoId, CancellationToken cancellationToken = default)
    {
        var response = await http.PostAsync($"api/eventos/{eventoId}/gerar-setores", null, cancellationToken);
        return await MapOperationEnvelopeAsync(response, cancellationToken);
    }

    public async Task<ApiResult> CancelarEventoAsync(int eventoId, CancellationToken cancellationToken = default)
    {
        var response = await http.PostAsync($"api/eventos/{eventoId}/cancelar", null, cancellationToken);
        return await MapOperationEnvelopeAsync(response, cancellationToken);
    }

    private static async Task<ApiResult<T>> MapDataEnvelopeAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var errPayload = await response.Content.ReadFromJsonAsync<ApiDataResponse<T>>(JsonOptions, cancellationToken);
            if (errPayload is not null && !string.IsNullOrWhiteSpace(errPayload.Message))
                return ApiResult<T>.Fail(errPayload.Message);
        }

        if (response.IsSuccessStatusCode)
        {
            var payload = await response.Content.ReadFromJsonAsync<ApiDataResponse<T>>(JsonOptions, cancellationToken);
            if (payload is null)
                return ApiResult<T>.Fail("Resposta inválida da API.");

            return payload.Success && payload.Data is not null
                ? ApiResult<T>.Ok(payload.Data)
                : ApiResult<T>.Fail(payload.Message);
        }

        return await MapResultAsync<T>(response, cancellationToken);
    }

    private static async Task<ApiResult> MapOperationEnvelopeAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var errPayload = await response.Content.ReadFromJsonAsync<ApiOperationResponse>(JsonOptions, cancellationToken);
            if (errPayload is not null && !string.IsNullOrWhiteSpace(errPayload.Message))
                return ApiResult.Fail(errPayload.Message);
        }

        if (response.IsSuccessStatusCode)
        {
            var payload = await response.Content.ReadFromJsonAsync<ApiOperationResponse>(JsonOptions, cancellationToken);
            if (payload is null)
                return ApiResult.Fail("Resposta inválida da API.");

            return payload.Success
                ? ApiResult.Ok()
                : ApiResult.Fail(payload.Message);
        }

        return await MapResultAsync(response, cancellationToken);
    }

    private static async Task<ApiResult<T>> MapResultAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            var data = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
            return ApiResult<T>.Ok(data!);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var message = string.IsNullOrWhiteSpace(body)
            ? $"Erro {(int)response.StatusCode}: {response.ReasonPhrase}"
            : body.Trim();
        return ApiResult<T>.Fail(message);
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
