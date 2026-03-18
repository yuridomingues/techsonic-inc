using Dapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Http;
using System;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var app = builder.Build();

// ================================
// ENDPOINTS
// ================================

app.MapGet("/api/eventos", async() => 
{
    using var connection = new SqlConnection(connectionString);
    var sql = "SELECT Id, Nome, CapacidadeTotal, DataEvento, PrecoPadrao FROM Eventos";

    var eventos = await connection.QueryAsync<Evento>(sql);
    return Results.Ok(eventos);
});

app.MapPost("/api/eventos", async(Evento evento) => 
{
    using var connection = new SqlConnection(connectionString);
    var sql = "INSERT INTO Eventos (Nome, CapacidadeTotal, DataEvento, PrecoPadrao) VALUES (@Nome, @CapacidadeTotal, @DataEvento, @PrecoPadrao)";

    await connection.ExecuteAsync(sql, evento);
    return Results.Created("/api/eventos", evento);
});

app.MapPost("/api/cupons", async (Cupom cupom) => 
{
    using var connection = new SqlConnection(connectionString);
    var sql = "INSERT INTO Cupons (Codigo, PorcentagemDesconto, ValorMinimoRegra) VALUES (@Codigo, @PorcentagemDesconto, @ValorMinimoRegra)";
    
    await connection.ExecuteAsync(sql, cupom);
    return Results.Created($"/api/cupons/{cupom.Codigo}", cupom);
});

app.MapPost("/api/usuarios", async (Usuario usuario) => 
{
    using var connection = new SqlConnection(connectionString);

    var checkSql = "SELECT COUNT(1) FROM Usuarios WHERE Cpf = @Cpf";
    var cpfExiste = await connection.ExecuteScalarAsync<int>(checkSql, new { Cpf = usuario.Cpf });

    if (cpfExiste > 0) 
    {
        return Results.BadRequest("Erro: Este CPF já está cadastrado.");
    }

    var insertSql = "INSERT INTO Usuarios (Cpf, Nome, Email) VALUES (@Cpf, @Nome, @Email)";
    await connection.ExecuteAsync(insertSql, usuario);

    return Results.Created($"/api/usuarios/{usuario.Cpf}", usuario);
});

app.Run();

// ==========================================
// MODELOS DE DADOS
// ==========================================
public record Evento(int Id, string Nome, int CapacidadeTotal, DateTime DataEvento, decimal PrecoPadrao);
public record Cupom(string Codigo, decimal PorcentagemDesconto, decimal ValorMinimoRegra);
public record Usuario(string Cpf, string Nome, string Email);