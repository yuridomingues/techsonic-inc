using TicketPrime.Web.Models;

namespace TicketPrime.Web.Services;

public interface IApiService
{
    Task<IReadOnlyList<EventoDto>> GetEventosAsync(CancellationToken cancellationToken = default);
    Task<ApiResult> PostEventoAsync(EventoCreateDto dto, CancellationToken cancellationToken = default);
    Task<ApiResult> PostCupomAsync(CupomDto dto, CancellationToken cancellationToken = default);
    Task<ApiResult> PostUsuarioAsync(UsuarioDto dto, CancellationToken cancellationToken = default);
}
