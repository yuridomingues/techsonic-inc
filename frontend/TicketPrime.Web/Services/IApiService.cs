using TicketPrime.Web.Models;

namespace TicketPrime.Web.Services;

public interface IApiService
{
    Task<IReadOnlyList<EventoDto>> GetEventosAsync(CancellationToken cancellationToken = default);
    Task<ApiResult<EventoDto>> PostEventoAsync(EventoCreateDto dto, CancellationToken cancellationToken = default);
    Task<ApiResult> PostCupomAsync(CupomDto dto, CancellationToken cancellationToken = default);
    Task<ApiResult<UsuarioCadastroResponse>> PostUsuarioAsync(UsuarioCreateDto dto, CancellationToken cancellationToken = default);
    Task<ApiResult<UsuarioEmailVerificationResponse>> VerifyUserEmailAsync(UsuarioEmailVerificationRequest dto, CancellationToken cancellationToken = default);
    Task<ApiResult<EmailVerificationDeliveryResponse>> ResendUserVerificationCodeAsync(UsuarioCodigoReenvioRequest dto, CancellationToken cancellationToken = default);

    Task<ApiResult<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AssentoDto>> GetAssentosAsync(int eventoId, CancellationToken cancellationToken = default);
    Task<AssentoLockResponse?> LockAssentoAsync(AssentoLockRequest request, CancellationToken cancellationToken = default);
    Task<ApiResult> ReleaseAssentoAsync(int seatId, CancellationToken cancellationToken = default);
    Task<ApiResult<FilaResponse>> JoinFilaAsync(FilaRequest request, CancellationToken cancellationToken = default);
    Task<ApiResult> LeaveFilaAsync(int eventoId, CancellationToken cancellationToken = default);
    Task<FilaDto?> GetFilaPositionAsync(int eventoId, CancellationToken cancellationToken = default);
    Task<ApiResult<ReservaDto>> CreateReservaAsync(ReservaCreateDto dto, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReservaDto>> GetReservasAsync(CancellationToken cancellationToken = default);
    Task<ApiResult<PagamentoDto>> SimulatePagamentoAsync(int reservaId, PagamentoCreateDto dto, CancellationToken cancellationToken = default);
    Task<ApiResult> CancelReservaAsync(int reservaId, CancellationToken cancellationToken = default);
    
    Task<AdminMetricas> GetAdminMetricasAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SetorDto>> GetSetoresAsync(int eventoId, CancellationToken cancellationToken = default);
    Task<ApiResult> PostSetorAsync(int eventoId, SetorCreateDto dto, CancellationToken cancellationToken = default);
    Task<ApiResult> GerarSetoresAsync(int eventoId, CancellationToken cancellationToken = default);
    Task<ApiResult> CancelarEventoAsync(int eventoId, CancellationToken cancellationToken = default);
}
