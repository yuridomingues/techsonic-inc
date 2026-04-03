namespace TicketPrime.Web.Models;

public record EventoDto(int Id, string Nome, int CapacidadeTotal, DateTime DataEvento, decimal PrecoPadrao);

public record EventoCreateDto(string Nome, int CapacidadeTotal, DateTime DataEvento, decimal PrecoPadrao);

public record CupomDto(string Codigo, decimal PorcentagemDesconto, decimal ValorMinimoRegra);

public record UsuarioDto(string Cpf, string Nome, string Email);
