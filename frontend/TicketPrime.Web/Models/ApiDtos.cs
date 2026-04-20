namespace TicketPrime.Web.Models;

public record EventoDto(int Id, string Nome, int CapacidadeTotal, DateTime DataEvento, decimal PrecoPadrao);

public record EventoCreateDto(string Nome, int CapacidadeTotal, DateTime DataEvento, decimal PrecoPadrao);

public record CupomDto(string Codigo, decimal PorcentagemDesconto, decimal ValorMinimoRegra);

public record UsuarioDto(string Cpf, string Nome, string Email);
public record UsuarioCreateDto(string Cpf, string Nome, string Email, string Senha);

public record LoginRequest(string Cpf, string Senha);
public record LoginResponse(string Token, string Cpf, string Nome, string Email, DateTime ExpiresAt);

public record AssentoDto(int Id, int EventoId, string Fila, string Numero, string Tipo, decimal PrecoAdicional, string Status, DateTime? LockedUntil, string? LockedByCpf);
public record AssentoLockRequest(int SeatId, int EventoId);
public record AssentoLockResponse(bool Success, string? Message, DateTime? LockedUntil);

public record FilaRequest(int EventoId);
public record FilaResponse(int EventoId, string UsuarioCpf, int Posicao, string Status, DateTime DataEntrada, int? TempoEstimado);
public record FilaDto(int Id, int EventoId, string UsuarioCpf, int Posicao, string Status, DateTime DataEntrada, int? TempoEstimado);

public record ReservaDto(int Id, string UsuarioCpf, int EventoId, string EventoNome, string? CupomUtilizado, decimal ValorFinalPago, string Status);
public record ReservaCreateDto(string UsuarioCpf, int EventoId, string? CupomUtilizado);

public record PagamentoDto(int Id, int ReservaId, string Metodo, decimal ValorTotal, string Status, string? TransacaoId, int? Parcelas, DateTime? DataPagamento, DateTime DataAtualizacao);
public record PagamentoCreateDto(string Metodo, int? Parcelas);
