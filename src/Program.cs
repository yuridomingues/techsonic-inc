using Dapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using System;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.SetIsOriginAllowed(origin =>
            !string.IsNullOrWhiteSpace(origin) &&
            Uri.TryCreate(origin, UriKind.Absolute, out var uri) &&
            (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
             uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)))
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors();

// ================================
// ENDPOINTS
// ================================

app.MapGet("/api/eventos", async() => 
{
    try
    {
        using var connection = new SqlConnection(connectionString);
        var sql = "SELECT Id, Nome, CapacidadeTotal, DataEvento, PrecoPadrao FROM Eventos ORDER BY DataEvento";

        var eventos = await connection.QueryAsync<EventoDto>(sql);
        return Results.Ok(eventos);
    }
    catch (SqlException)
    {
        return Results.Problem(
            title: "Falha de banco de dados",
            detail: "Não foi possível conectar ao SQL Server. Verifique a connection string e se o banco está ativo.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapPost("/api/eventos", async(EventoCreateDto evento) => 
{
    if (!ValidacoesEntrada.NomeObrigatorio(evento.Nome))
        return Results.BadRequest("Nome do evento é obrigatório.");

    if (!ValidacoesEntrada.DataEventoFutura(evento.DataEvento))
        return Results.BadRequest("A data do evento deve ser futura.");

    if (!ValidacoesEntrada.CapacidadePositiva(evento.CapacidadeTotal))
        return Results.BadRequest("A capacidade total deve ser positiva.");

    if (!ValidacoesEntrada.PrecoNaoNegativo(evento.PrecoPadrao))
        return Results.BadRequest("O preço padrão não pode ser negativo.");

    try
    {
        using var connection = new SqlConnection(connectionString);
        var sql = @"
            INSERT INTO Eventos (Nome, CapacidadeTotal, DataEvento, PrecoPadrao)
            OUTPUT INSERTED.Id
            VALUES (@Nome, @CapacidadeTotal, @DataEvento, @PrecoPadrao)";

        var id = await connection.ExecuteScalarAsync<int>(sql, evento);
        var criado = new EventoDto(id, evento.Nome.Trim(), evento.CapacidadeTotal, evento.DataEvento, evento.PrecoPadrao);

        return Results.Created($"/api/eventos/{id}", criado);
    }
    catch (SqlException)
    {
        return Results.Problem(
            title: "Falha de banco de dados",
            detail: "Não foi possível conectar ao SQL Server. Verifique a connection string e se o banco está ativo.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapPost("/api/cupons", async (CupomCreateDto cupom) => 
{
    if (!ValidacoesEntrada.NomeObrigatorio(cupom.Codigo))
        return Results.BadRequest("Código do cupom é obrigatório.");

    if (!ValidacoesEntrada.PercentualValido(cupom.PorcentagemDesconto))
        return Results.BadRequest("A porcentagem de desconto deve estar entre 0 e 100.");

    if (!ValidacoesEntrada.PrecoNaoNegativo(cupom.ValorMinimoRegra))
        return Results.BadRequest("O valor mínimo não pode ser negativo.");

    try
    {
        using var connection = new SqlConnection(connectionString);
        var sql = "INSERT INTO Cupons (Codigo, PorcentagemDesconto, ValorMinimoRegra) VALUES (@Codigo, @PorcentagemDesconto, @ValorMinimoRegra)";
        var payload = new
        {
            Codigo = cupom.Codigo.Trim().ToUpperInvariant(),
            cupom.PorcentagemDesconto,
            cupom.ValorMinimoRegra,
        };
        
        await connection.ExecuteAsync(sql, payload);
        return Results.Created($"/api/cupons/{payload.Codigo}", payload);
    }
    catch (SqlException)
    {
        return Results.Problem(
            title: "Falha de banco de dados",
            detail: "Não foi possível conectar ao SQL Server. Verifique a connection string e se o banco está ativo.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapPost("/api/usuarios", async (Usuario usuario) => 
{
    var cpfLimpo = new string((usuario.Cpf ?? string.Empty).Where(char.IsDigit).ToArray());
    if (!ValidacoesEntrada.CpfTem11Digitos(cpfLimpo))
        return Results.BadRequest("Erro: CPF inválido. Informe 11 dígitos.");

    if (!ValidacoesEntrada.NomeObrigatorio(usuario.Nome) || !ValidacoesEntrada.EmailObrigatorio(usuario.Email))
        return Results.BadRequest("Erro: Nome e e-mail são obrigatórios.");

    try
    {
        using var connection = new SqlConnection(connectionString);
        var checkSql = "SELECT COUNT(1) FROM Usuarios WHERE Cpf = @Cpf";
        var cpfExiste = await connection.ExecuteScalarAsync<int>(checkSql, new { Cpf = cpfLimpo });

        if (cpfExiste > 0) 
        {
            return Results.BadRequest("Erro: Este CPF já está cadastrado.");
        }

        var insertSql = "INSERT INTO Usuarios (Cpf, Nome, Email) VALUES (@Cpf, @Nome, @Email)";
        var novoUsuario = new Usuario(cpfLimpo, usuario.Nome.Trim(), usuario.Email.Trim());
        await connection.ExecuteAsync(insertSql, novoUsuario);

        return Results.Created($"/api/usuarios/{novoUsuario.Cpf}", novoUsuario);
    }
    catch (SqlException)
    {
        return Results.Problem(
            title: "Falha de banco de dados",
            detail: "Não foi possível conectar ao SQL Server. Verifique a connection string e se o banco está ativo.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.Run();

// ==========================================
// MODELOS DE DADOS
// ==========================================
public record EventoDto(int Id, string Nome, int CapacidadeTotal, DateTime DataEvento, decimal PrecoPadrao);
public record EventoCreateDto(string Nome, int CapacidadeTotal, DateTime DataEvento, decimal PrecoPadrao);
public record CupomCreateDto(string Codigo, decimal PorcentagemDesconto, decimal ValorMinimoRegra);
public record Usuario(string Cpf, string Nome, string Email);
