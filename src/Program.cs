using Dapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Linq;
using System.Text;
using System.Security.Claims;
using System.Security.Cryptography;
using System.IdentityModel.Tokens.Jwt;
using System.Globalization;
using BCrypt.Net;
using TicketPrime.Server;
using TicketPrime.Server.Hubs;
using TicketPrime.Server.Services;
using Serilog;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

const string AdminCpfPadrao = "00000000000";
const string AdminSenhaHashLegadoInvalido = "$2a$11$K7Z5Y5Q5Z5Y5Q5Z5Y5Q5Z5Y5Q5Z5Y5Q5Z5Y5Q5Z5Y5Q5Z5Y5Q5Z5Y";
const string AdminSenhaHashPadrao = "$2a$11$VsP1Gl7H66fBSngtBZoYTeM6j2e05rwDo7d35OfXaDHf2INtgcGv6";
const int MaxTentativasCodigoEmail = 5;
const int IntervaloMinimoReenvioCodigoSegundos = 60;
const int QuantidadeMaximaAssentosPadraoPorEvento = 240;
const int AssentosPorFilaPadrao = 10;
const int MinutosExpiracaoReservaPendente = 15;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/ticketprime-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

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

builder.Services.AddHealthChecks();
builder.Services.Configure<EmailDeliveryOptions>(builder.Configuration.GetSection("Email"));
builder.Services.AddScoped<IEmailVerificationSender, EmailVerificationSender>();

// JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"];
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];
var jwtExpiryMinutes = builder.Configuration.GetValue<int>("Jwt:ExpiryMinutes", 60);

if (string.IsNullOrEmpty(jwtKey)) throw new InvalidOperationException("JWT Key is missing in configuration.");
if (string.IsNullOrEmpty(jwtIssuer)) throw new InvalidOperationException("JWT Issuer is missing in configuration.");
if (string.IsNullOrEmpty(jwtAudience)) throw new InvalidOperationException("JWT Audience is missing in configuration.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey!)),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddSignalR();

builder.WebHost.UseStaticWebAssets();

var app = builder.Build();

await GarantirEstruturaVerificacaoEmailAsync(connectionString);
await CorrigirHashAdminPadraoAsync(connectionString);
await GarantirMapaPadraoAssentosAsync(connectionString);

static string NormalizarCpf(string? cpf) => new((cpf ?? string.Empty).Where(char.IsDigit).ToArray());
static string NormalizarEmail(string? email) => (email ?? string.Empty).Trim().ToLowerInvariant();
static string NormalizarNome(string? nome) => string.Join(' ', (nome ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries));

static string GerarCodigoVerificacao()
{
    var numero = RandomNumberGenerator.GetInt32(0, 1_000_000);
    return numero.ToString($"D{ValidacoesEntrada.TamanhoCodigoVerificacao}");
}

static string HashCodigoVerificacao(string codigo)
{
    var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(codigo));
    return Convert.ToHexString(bytes);
}

static async Task<bool> ColunaExisteAsync(SqlConnection connection, string tabela, string coluna)
{
    var total = await connection.ExecuteScalarAsync<int>(
        @"SELECT COUNT(1)
          FROM sys.columns
          WHERE Name = @Coluna
            AND Object_ID = Object_ID(@Tabela)",
        new { Tabela = tabela, Coluna = coluna });

    return total > 0;
}

static async Task GarantirEstruturaVerificacaoEmailAsync(string? connectionString)
{
    if (string.IsNullOrWhiteSpace(connectionString))
        return;

    try
    {
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        var emailVerificadoAdicionado = false;

        if (!await ColunaExisteAsync(connection, "Usuarios", "EmailVerificado"))
        {
            await connection.ExecuteAsync("ALTER TABLE Usuarios ADD EmailVerificado BIT NULL");
            await connection.ExecuteAsync("UPDATE Usuarios SET EmailVerificado = 0 WHERE EmailVerificado IS NULL");
            await connection.ExecuteAsync("ALTER TABLE Usuarios ALTER COLUMN EmailVerificado BIT NOT NULL");
            await connection.ExecuteAsync("ALTER TABLE Usuarios ADD CONSTRAINT DF_Usuarios_EmailVerificado DEFAULT 0 FOR EmailVerificado");
            emailVerificadoAdicionado = true;
        }

        if (!await ColunaExisteAsync(connection, "Usuarios", "EmailVerificadoEm"))
            await connection.ExecuteAsync("ALTER TABLE Usuarios ADD EmailVerificadoEm DATETIME NULL");

        if (!await ColunaExisteAsync(connection, "Usuarios", "CodigoVerificacaoHash"))
            await connection.ExecuteAsync("ALTER TABLE Usuarios ADD CodigoVerificacaoHash VARCHAR(64) NULL");

        if (!await ColunaExisteAsync(connection, "Usuarios", "CodigoVerificacaoExpiraEm"))
            await connection.ExecuteAsync("ALTER TABLE Usuarios ADD CodigoVerificacaoExpiraEm DATETIME NULL");

        if (!await ColunaExisteAsync(connection, "Usuarios", "TentativasCodigoEmail"))
        {
            await connection.ExecuteAsync("ALTER TABLE Usuarios ADD TentativasCodigoEmail INT NULL");
            await connection.ExecuteAsync("UPDATE Usuarios SET TentativasCodigoEmail = 0 WHERE TentativasCodigoEmail IS NULL");
            await connection.ExecuteAsync("ALTER TABLE Usuarios ALTER COLUMN TentativasCodigoEmail INT NOT NULL");
            await connection.ExecuteAsync("ALTER TABLE Usuarios ADD CONSTRAINT DF_Usuarios_TentativasCodigoEmail DEFAULT 0 FOR TentativasCodigoEmail");
        }

        if (!await ColunaExisteAsync(connection, "Usuarios", "UltimoEnvioCodigoEm"))
            await connection.ExecuteAsync("ALTER TABLE Usuarios ADD UltimoEnvioCodigoEm DATETIME NULL");

        if (emailVerificadoAdicionado)
        {
            await connection.ExecuteAsync(
                @"UPDATE Usuarios
                  SET EmailVerificado = 1,
                      EmailVerificadoEm = COALESCE(EmailVerificadoEm, GETUTCDATE())");
        }

        await connection.ExecuteAsync(
            @"UPDATE Usuarios
              SET EmailVerificado = 1,
                  EmailVerificadoEm = COALESCE(EmailVerificadoEm, GETUTCDATE()),
                  CodigoVerificacaoHash = NULL,
                  CodigoVerificacaoExpiraEm = NULL,
                  TentativasCodigoEmail = 0,
                  UltimoEnvioCodigoEm = NULL
              WHERE IsAdmin = 1");
    }
    catch (SqlException ex)
    {
        Log.Warning(ex, "Nao foi possivel garantir a estrutura de verificacao de e-mail na inicializacao.");
    }
}

static async Task CorrigirHashAdminPadraoAsync(string? connectionString)
{
    if (string.IsNullOrWhiteSpace(connectionString))
        return;

    try
    {
        using var connection = new SqlConnection(connectionString);
        await connection.ExecuteAsync(
            @"UPDATE Usuarios
              SET SenhaHash = @NovoHash
              WHERE Cpf = @Cpf AND SenhaHash = @HashAntigo",
            new
            {
                Cpf = AdminCpfPadrao,
                HashAntigo = AdminSenhaHashLegadoInvalido,
                NovoHash = AdminSenhaHashPadrao,
            });
    }
    catch (SqlException ex)
    {
        Log.Warning(ex, "Não foi possível validar o hash do admin padrão na inicialização.");
    }
}

static async Task<bool> UsuarioEhAdminAsync(SqlConnection connection, string cpf)
{
    var total = await connection.ExecuteScalarAsync<int>(
        "SELECT COUNT(1) FROM Usuarios WHERE Cpf = @Cpf AND IsAdmin = 1",
        new { Cpf = cpf });

    return total > 0;
}

static async Task GarantirMapaPadraoAssentosAsync(string? connectionString)
{
    if (string.IsNullOrWhiteSpace(connectionString))
        return;

    try
    {
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        var eventoIds = (await connection.QueryAsync<int>(
            @"SELECT e.Id
              FROM Eventos e
              WHERE NOT EXISTS (
                  SELECT 1
                  FROM Assentos a
                  WHERE a.EventoId = e.Id)"))
            .ToArray();

        foreach (var eventoId in eventoIds)
            await GarantirMapaPadraoAssentosParaEventoAsync(connection, eventoId);
    }
    catch (SqlException ex)
    {
        Log.Warning(ex, "Nao foi possivel garantir o mapa padrao de assentos na inicializacao.");
    }
}

static async Task GarantirMapaPadraoAssentosParaEventoAsync(SqlConnection connection, int eventoId, SqlTransaction? transaction = null)
{
    var evento = await connection.QuerySingleOrDefaultAsync<EventoMapaAssentosLookup>(
        @"SELECT Id, CapacidadeTotal, PrecoPadrao
          FROM Eventos
          WHERE Id = @EventoId",
        new { EventoId = eventoId },
        transaction);
    if (evento is null)
        return;

    var totalAssentos = await connection.ExecuteScalarAsync<int>(
        "SELECT COUNT(1) FROM Assentos WHERE EventoId = @EventoId",
        new { EventoId = eventoId },
        transaction);
    if (totalAssentos > 0)
        return;

    var quantidadeAssentos = Math.Min(Math.Max(evento.CapacidadeTotal, 0), QuantidadeMaximaAssentosPadraoPorEvento);
    if (quantidadeAssentos <= 0)
        return;

    var totalSetores = await connection.ExecuteScalarAsync<int>(
        "SELECT COUNT(1) FROM Setores WHERE EventoId = @EventoId",
        new { EventoId = eventoId },
        transaction);

    if (totalSetores == 0)
    {
        var capacidadePremium = (int)Math.Ceiling(quantidadeAssentos * 0.20m);
        var capacidadeVip = (int)Math.Ceiling(quantidadeAssentos * 0.30m);
        var capacidadeRegular = Math.Max(0, quantidadeAssentos - capacidadePremium - capacidadeVip);
        var setores = new[]
        {
            new { EventoId = eventoId, Nome = "Premium", Preco = decimal.Round(evento.PrecoPadrao * 1.30m, 2, MidpointRounding.AwayFromZero), Cor = "#F97316", Capacidade = (int?)capacidadePremium, Ordem = 1 },
            new { EventoId = eventoId, Nome = "Vip", Preco = decimal.Round(evento.PrecoPadrao * 1.15m, 2, MidpointRounding.AwayFromZero), Cor = "#22D3EE", Capacidade = (int?)capacidadeVip, Ordem = 2 },
            new { EventoId = eventoId, Nome = "Regular", Preco = evento.PrecoPadrao, Cor = "#34D399", Capacidade = (int?)capacidadeRegular, Ordem = 3 },
        };

        await connection.ExecuteAsync(
            @"INSERT INTO Setores (EventoId, Nome, Preco, Cor, Capacidade, Ordem)
              VALUES (@EventoId, @Nome, @Preco, @Cor, @Capacidade, @Ordem)",
            setores,
            transaction);
    }

    var totalFilas = (int)Math.Ceiling(quantidadeAssentos / (decimal)AssentosPorFilaPadrao);
    var assentos = new List<object>(quantidadeAssentos);
    var contador = 0;

    for (var indiceFila = 0; indiceFila < totalFilas; indiceFila++)
    {
        var fila = ObterNomeFilaPadrao(indiceFila);
        var tipo = ObterTipoAssentoPadrao(indiceFila, totalFilas);
        var precoAdicional = ObterPrecoAdicionalPadrao(tipo, evento.PrecoPadrao);

        for (var numero = 1; numero <= AssentosPorFilaPadrao && contador < quantidadeAssentos; numero++)
        {
            contador++;
            assentos.Add(new
            {
                EventoId = eventoId,
                Fila = fila,
                Numero = numero.ToString("D2", CultureInfo.InvariantCulture),
                Tipo = tipo,
                PrecoAdicional = precoAdicional,
            });
        }
    }

    await connection.ExecuteAsync(
        @"INSERT INTO Assentos (EventoId, Fila, Numero, Tipo, PrecoAdicional)
          VALUES (@EventoId, @Fila, @Numero, @Tipo, @PrecoAdicional)",
        assentos,
        transaction);
}

static async Task LiberarReservasPendentesExpiradasAsync(SqlConnection connection, int eventoId, SqlTransaction? transaction = null)
{
    var reservasExpiradas = (await connection.QueryAsync<int>(
        @"SELECT DISTINCT r.Id
          FROM Reservas r
          INNER JOIN Tickets t ON t.ReservaId = r.Id
          INNER JOIN Assentos a ON a.Id = t.AssentoId
          WHERE r.EventoId = @EventoId
            AND r.Status = 'pendente'
            AND a.Status = 'reservado'
            AND a.LockedUntil IS NOT NULL
            AND a.LockedUntil < GETUTCDATE()",
        new { EventoId = eventoId },
        transaction))
        .Distinct()
        .ToArray();

    if (reservasExpiradas.Length > 0)
    {
        await connection.ExecuteAsync(
            "UPDATE Tickets SET Status = 'cancelado' WHERE ReservaId IN @ReservaIds",
            new { ReservaIds = reservasExpiradas },
            transaction);

        await connection.ExecuteAsync(
            "UPDATE Reservas SET Status = 'cancelada' WHERE Id IN @ReservaIds AND Status = 'pendente'",
            new { ReservaIds = reservasExpiradas },
            transaction);
    }

    await connection.ExecuteAsync(
        @"UPDATE Assentos
          SET Status = 'disponivel',
              LockedUntil = NULL,
              LockedByCpf = NULL
          WHERE EventoId = @EventoId
            AND Status = 'reservado'
            AND LockedUntil IS NOT NULL
            AND LockedUntil < GETUTCDATE()",
        new { EventoId = eventoId },
        transaction);
}

static string ObterNomeFilaPadrao(int indice)
{
    const string letras = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    var nome = string.Empty;
    var valor = indice;

    do
    {
        nome = letras[valor % letras.Length] + nome;
        valor = (valor / letras.Length) - 1;
    }
    while (valor >= 0);

    return nome;
}

static string ObterTipoAssentoPadrao(int indiceFila, int totalFilas)
{
    var limitePremium = Math.Max(1, (int)Math.Ceiling(totalFilas * 0.20m));
    var limiteVip = Math.Max(limitePremium + 1, (int)Math.Ceiling(totalFilas * 0.45m));

    if (indiceFila < limitePremium)
        return "premium";

    if (indiceFila < limiteVip)
        return "vip";

    return "regular";
}

static decimal ObterPrecoAdicionalPadrao(string tipoAssento, decimal precoPadrao)
{
    return tipoAssento switch
    {
        "premium" => decimal.Round(precoPadrao * 0.30m, 2, MidpointRounding.AwayFromZero),
        "vip" => decimal.Round(precoPadrao * 0.15m, 2, MidpointRounding.AwayFromZero),
        _ => 0m,
    };
}

static decimal[] DistribuirValorTickets(int quantidade, decimal valorTotal)
{
    if (quantidade <= 0)
        return [];

    var valores = new decimal[quantidade];
    var valorBase = decimal.Round(valorTotal / quantidade, 2, MidpointRounding.AwayFromZero);
    var acumulado = 0m;

    for (var index = 0; index < quantidade - 1; index++)
    {
        valores[index] = valorBase;
        acumulado += valorBase;
    }

    valores[^1] = decimal.Round(valorTotal - acumulado, 2, MidpointRounding.AwayFromZero);
    return valores;
}

static string MontarResumoAssentos(IEnumerable<AssentoReservaSelecionado> assentos)
{
    return string.Join(", ", assentos
        .OrderBy(assento => assento.Fila, StringComparer.OrdinalIgnoreCase)
        .ThenBy(assento => assento.Numero, StringComparer.OrdinalIgnoreCase)
        .Select(assento => $"{assento.Fila}{assento.Numero}"));
}

app.UseRouting();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapHub<SeatHub>("/hubs/seat");

app.UseStaticFiles();
app.UseBlazorFrameworkFiles();

// ================================
// ENDPOINTS
// ================================

app.MapGet("/api/eventos", async() => 
{
    try
    {
        using var connection = new SqlConnection(connectionString);
        var sql = "SELECT Id, Nome, CapacidadeTotal, DataEvento, PrecoPadrao, TipoEvento, Descricao, LocalNome, LocalCidade, BannerUrl, GaleriaTexto, TaxaFixa, Status FROM Eventos ORDER BY DataEvento";

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

app.MapPost("/api/eventos", async (EventoCreateDto evento, HttpContext httpContext) => 
{
    var cpf = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrEmpty(cpf))
        return Results.Unauthorized();

    if (!ValidacoesEntrada.NomeObrigatorio(evento.Nome))
        return Results.Ok(new ApiDataResponse<EventoDto>(false, "Nome do evento é obrigatório.", null));

    if (!ValidacoesEntrada.DataEventoFutura(evento.DataEvento))
        return Results.Ok(new ApiDataResponse<EventoDto>(false, "A data do evento deve ser futura.", null));

    if (!ValidacoesEntrada.CapacidadePositiva(evento.CapacidadeTotal))
        return Results.Ok(new ApiDataResponse<EventoDto>(false, "A capacidade total deve ser positiva.", null));

    if (!ValidacoesEntrada.PrecoPositivo(evento.PrecoPadrao))
        return Results.Ok(new ApiDataResponse<EventoDto>(false, "O preço padrão deve ser maior que zero.", null));

    if (!ValidacoesEntrada.TipoEventoValido(evento.TipoEvento))
        return Results.Ok(new ApiDataResponse<EventoDto>(false, "Tipo de evento inválido.", null));

    var taxaFixa = evento.TaxaFixa ?? 5.00m;
    if (!ValidacoesEntrada.PrecoNaoNegativo(taxaFixa))
        return Results.Ok(new ApiDataResponse<EventoDto>(false, "A taxa fixa não pode ser negativa.", null));

    try
    {
        using var connection = new SqlConnection(connectionString);
        if (!await UsuarioEhAdminAsync(connection, cpf))
            return Results.Forbid();

        var sql = @"
            INSERT INTO Eventos (Nome, CapacidadeTotal, DataEvento, PrecoPadrao, TipoEvento, Descricao, LocalNome, LocalCidade, BannerUrl, GaleriaTexto, TaxaFixa, Status)
            OUTPUT INSERTED.Id
            VALUES (@Nome, @CapacidadeTotal, @DataEvento, @PrecoPadrao, @TipoEvento, @Descricao, @LocalNome, @LocalCidade, @BannerUrl, @GaleriaTexto, @TaxaFixa, @Status)";

        var tipoEvento = string.IsNullOrWhiteSpace(evento.TipoEvento)
            ? null
            : evento.TipoEvento.Trim().ToLowerInvariant();
        var status = string.IsNullOrWhiteSpace(evento.Status)
            ? "ativo"
            : evento.Status.Trim().ToLowerInvariant();

        var parametros = new
        {
            evento.Nome,
            evento.CapacidadeTotal,
            evento.DataEvento,
            evento.PrecoPadrao,
            TipoEvento = tipoEvento,
            evento.Descricao,
            evento.LocalNome,
            evento.LocalCidade,
            evento.BannerUrl,
            evento.GaleriaTexto,
            TaxaFixa = taxaFixa,
            Status = status,
        };
        var id = await connection.ExecuteScalarAsync<int>(sql, parametros);
        var criado = new EventoDto(id, evento.Nome.Trim(), evento.CapacidadeTotal, evento.DataEvento, evento.PrecoPadrao, tipoEvento, evento.Descricao, evento.LocalNome, evento.LocalCidade, evento.BannerUrl, evento.GaleriaTexto, taxaFixa, status);

        return Results.Ok(new ApiDataResponse<EventoDto>(true, "Evento cadastrado com sucesso.", criado));
    }
    catch (SqlException)
    {
        return Results.Problem(
            title: "Falha de banco de dados",
            detail: "Não foi possível conectar ao SQL Server. Verifique a connection string e se o banco está ativo.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}).RequireAuthorization();

// ================================
// ENDPOINTS DE SETORES
// ================================
app.MapGet("/api/eventos/{eventoId}/setores", async (int eventoId) =>
{
    try
    {
        using var connection = new SqlConnection(connectionString);
        var sql = "SELECT Id, EventoId, Nome, Preco, Cor, Capacidade, Ordem FROM Setores WHERE EventoId = @EventoId ORDER BY Ordem";
        var setores = await connection.QueryAsync<SetorDto>(sql, new { EventoId = eventoId });
        return Results.Ok(setores);
    }
    catch (SqlException)
    {
        return Results.Problem(
            title: "Falha de banco de dados",
            detail: "Não foi possível conectar ao SQL Server.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapPost("/api/eventos/{eventoId}/setores", async (int eventoId, SetorCreateDto setor, HttpContext httpContext) =>
{
    var cpf = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrEmpty(cpf))
        return Results.Unauthorized();

    if (string.IsNullOrWhiteSpace(setor.Nome))
        return Results.Ok(new ApiOperationResponse(false, "Nome do setor é obrigatório."));
    if (setor.Preco < 0)
        return Results.Ok(new ApiOperationResponse(false, "Preço não pode ser negativo."));
    try
    {
        using var connection = new SqlConnection(connectionString);
        if (!await UsuarioEhAdminAsync(connection, cpf))
            return Results.Forbid();

        // Verificar se evento existe
        var eventoExiste = await connection.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM Eventos WHERE Id = @EventoId", new { EventoId = eventoId });
        if (eventoExiste == 0)
            return Results.Ok(new ApiOperationResponse(false, "Evento não encontrado."));
        
        var sql = @"INSERT INTO Setores (EventoId, Nome, Preco, Cor, Capacidade, Ordem)
                    OUTPUT INSERTED.Id
                    VALUES (@EventoId, @Nome, @Preco, @Cor, @Capacidade, @Ordem)";
        var id = await connection.ExecuteScalarAsync<int>(sql, new
        {
            EventoId = eventoId,
            setor.Nome,
            setor.Preco,
            setor.Cor,
            setor.Capacidade,
            setor.Ordem
        });
        return Results.Ok(new ApiOperationResponse(true, "Setor cadastrado com sucesso."));
    }
    catch (SqlException)
    {
        return Results.Problem(
            title: "Falha de banco de dados",
            detail: "Não foi possível conectar ao SQL Server.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}).RequireAuthorization();

app.MapPost("/api/eventos/{eventoId}/gerar-setores", async (int eventoId, HttpContext httpContext) =>
{
    var cpf = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrEmpty(cpf))
        return Results.Unauthorized();

    try
    {
        using var connection = new SqlConnection(connectionString);
        if (!await UsuarioEhAdminAsync(connection, cpf))
            return Results.Forbid();

        var evento = await connection.QuerySingleOrDefaultAsync<EventoPrecoLookup>(
            "SELECT Id, PrecoPadrao FROM Eventos WHERE Id = @EventoId",
            new { EventoId = eventoId });
        if (evento == null)
            return Results.Ok(new ApiOperationResponse(false, "Evento não encontrado."));

        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();
        await GarantirMapaPadraoAssentosParaEventoAsync(connection, eventoId, transaction);
        transaction.Commit();

        return Results.Ok(new ApiOperationResponse(true, "Setores e assentos padrão gerados com sucesso."));
    }
    catch (SqlException)
    {
        return Results.Problem(
            title: "Falha de banco de dados",
            detail: "Não foi possível conectar ao SQL Server.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}).RequireAuthorization();

app.MapDelete("/api/setores/{id}", async (int id, HttpContext httpContext) =>
{
    var cpf = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrEmpty(cpf))
        return Results.Unauthorized();

    try
    {
        using var connection = new SqlConnection(connectionString);
        if (!await UsuarioEhAdminAsync(connection, cpf))
            return Results.Forbid();

        var deleted = await connection.ExecuteAsync("DELETE FROM Setores WHERE Id = @Id", new { Id = id });
        if (deleted == 0)
            return Results.Ok(new ApiOperationResponse(false, "Setor não encontrado."));
        return Results.Ok(new ApiOperationResponse(true, "Setor excluído com sucesso."));
    }
    catch (SqlException)
    {
        return Results.Problem(
            title: "Falha de banco de dados",
            detail: "Não foi possível conectar ao SQL Server.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}).RequireAuthorization();

// ================================
// ENDPOINT DE CANCELAMENTO DE EVENTO (ADMIN)
// ================================
app.MapPost("/api/eventos/{id}/cancelar", async (int id, HttpContext httpContext) =>
{
    var cpf = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrEmpty(cpf))
        return Results.Unauthorized();
    
    try
    {
        using var connection = new SqlConnection(connectionString);
        if (!await UsuarioEhAdminAsync(connection, cpf))
            return Results.Forbid();
        
        // Verificar se evento existe e está ativo
        var evento = await connection.QuerySingleOrDefaultAsync<EventoStatusLookup>(
            "SELECT Id, Status FROM Eventos WHERE Id = @Id",
            new { Id = id });
        if (evento == null)
            return Results.Ok(new ApiOperationResponse(false, "Evento não encontrado."));
        if (evento.Status == "cancelado")
            return Results.Ok(new ApiOperationResponse(false, "Evento já está cancelado."));
        
        // Atualizar status do evento para cancelado
        await connection.ExecuteAsync(
            "UPDATE Eventos SET Status = 'cancelado' WHERE Id = @Id",
            new { Id = id });
        
        // Cancelar todas as reservas do evento (chamar lógica de cancelamento de reserva)
        var reservas = await connection.QueryAsync<ReservaStatusLookup>(
            "SELECT Id, UsuarioCpf, EventoId, ValorFinalPago, Status FROM Reservas WHERE EventoId = @EventoId AND Status != 'cancelada'",
            new { EventoId = id });
        
        foreach (var reserva in reservas)
        {
            // Atualizar status da reserva para cancelada
            await connection.ExecuteAsync(
                "UPDATE Reservas SET Status = 'cancelada' WHERE Id = @Id",
                new { Id = reserva.Id });
            
            // Se houver pagamento aprovado, estornar
            var pagamento = await connection.QuerySingleOrDefaultAsync<PagamentoStatusLookup>(
                "SELECT Id, Status FROM Pagamentos WHERE ReservaId = @ReservaId",
                new { ReservaId = reserva.Id });
            if (pagamento != null && pagamento.Status == "aprovado")
            {
                await connection.ExecuteAsync(
                    "UPDATE Pagamentos SET Status = 'estornado', DataAtualizacao = GETDATE() WHERE Id = @Id",
                    new { Id = pagamento.Id });
            }
            
            // Gerar cupom de 10% para o usuário
            var cupomCodigo = $"CANCEL-EVT-{reserva.Id}-{DateTime.UtcNow:yyyyMMddHHmm}";
            await connection.ExecuteAsync(
                "INSERT INTO Cupons (Codigo, PorcentagemDesconto, ValorMinimoRegra, DataExpiracao) VALUES (@Codigo, 10.00, 0.00, DATEADD(month, 3, GETDATE()))",
                new { Codigo = cupomCodigo });
        }
        
        return Results.Ok(new ApiOperationResponse(true, "Evento cancelado com sucesso. Reservas canceladas e cupons gerados."));
    }
    catch (SqlException)
    {
        return Results.Problem(
            title: "Falha de banco de dados",
            detail: "Não foi possível conectar ao SQL Server.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}).RequireAuthorization();

// ================================
// ENDPOINT DE MÉTRICAS (ADMIN)
// ================================
app.MapGet("/api/admin/metricas", async (HttpContext httpContext) =>
{
    var cpf = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrEmpty(cpf))
        return Results.Unauthorized();
    
    try
    {
        using var connection = new SqlConnection(connectionString);
        if (!await UsuarioEhAdminAsync(connection, cpf))
            return Results.Forbid();
        
        // Total de ingressos vendidos (reservas confirmadas)
        var ingressosVendidos = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Reservas WHERE Status = 'confirmada'");
        
        // Receita total (soma dos valores pagos em reservas confirmadas)
        var receitaTotal = await connection.ExecuteScalarAsync<decimal>(
            "SELECT ISNULL(SUM(ValorFinalPago), 0) FROM Reservas WHERE Status = 'confirmada'");
        
        // Total de eventos ativos
        var eventosAtivos = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Eventos WHERE Status = 'ativo'");
        
        // Total de usuários cadastrados
        var totalUsuarios = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Usuarios");
        
        return Results.Ok(new
        {
            ingressosVendidos,
            receitaTotal,
            eventosAtivos,
            totalUsuarios
        });
    }
    catch (SqlException)
    {
        return Results.Problem(
            title: "Falha de banco de dados",
            detail: "Não foi possível conectar ao SQL Server.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}).RequireAuthorization();

app.MapPost("/api/cupons", async (CupomCreateDto cupom, HttpContext httpContext) =>
{
    var cpf = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrEmpty(cpf))
        return Results.Unauthorized();

    if (!ValidacoesEntrada.NomeObrigatorio(cupom.Codigo))
        return Results.Ok(new ApiOperationResponse(false, "Código do cupom é obrigatório."));

    if (!ValidacoesEntrada.PercentualValido(cupom.PorcentagemDesconto))
        return Results.Ok(new ApiOperationResponse(false, "A porcentagem de desconto deve estar entre 0 e 100."));

    if (!ValidacoesEntrada.PrecoNaoNegativo(cupom.ValorMinimoRegra))
        return Results.Ok(new ApiOperationResponse(false, "O valor mínimo não pode ser negativo."));

    try
    {
        using var connection = new SqlConnection(connectionString);
        if (!await UsuarioEhAdminAsync(connection, cpf))
            return Results.Forbid();

        var sql = "INSERT INTO Cupons (Codigo, PorcentagemDesconto, ValorMinimoRegra) VALUES (@Codigo, @PorcentagemDesconto, @ValorMinimoRegra)";
        var payload = new
        {
            Codigo = cupom.Codigo.Trim().ToUpperInvariant(),
            cupom.PorcentagemDesconto,
            cupom.ValorMinimoRegra,
        };
        
        await connection.ExecuteAsync(sql, payload);
        return Results.Ok(new ApiOperationResponse(true, "Cupom cadastrado com sucesso."));
    }
    catch (SqlException ex) when (ex.Number is 2601 or 2627)
    {
        return Results.Ok(new ApiOperationResponse(false, "Já existe um cupom com este código."));
    }
    catch (SqlException)
    {
        return Results.Problem(
            title: "Falha de banco de dados",
            detail: "Não foi possível conectar ao SQL Server. Verifique a connection string e se o banco está ativo.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}).RequireAuthorization();

app.MapPost("/api/auth/login", async (LoginRequest login) =>
{
    var cpfLimpo = NormalizarCpf(login.Cpf);
    if (!ValidacoesEntrada.CpfTem11Digitos(cpfLimpo))
        return Results.Ok(new LoginAttemptResponse(false, "CPF inválido.", null));

    if (string.IsNullOrWhiteSpace(login.Senha))
        return Results.Ok(new LoginAttemptResponse(false, "Senha é obrigatória.", null));

    try
    {
        using var connection = new SqlConnection(connectionString);
        var sql = "SELECT Cpf, Nome, Email, SenhaHash, IsAdmin, EmailVerificado FROM Usuarios WHERE Cpf = @Cpf";
        var usuario = await connection.QuerySingleOrDefaultAsync<UsuarioLoginDb>(sql, new { Cpf = cpfLimpo });
        if (usuario == null || string.IsNullOrWhiteSpace(usuario.SenhaHash))
            return Results.Ok(new LoginAttemptResponse(false, "CPF ou senha invalidos.", null));

        if (!BCrypt.Net.BCrypt.Verify(login.Senha, usuario.SenhaHash))
            return Results.Ok(new LoginAttemptResponse(false, "CPF ou senha invalidos.", null));

        if (!usuario.EmailVerificado)
            return Results.Ok(new LoginAttemptResponse(false, "Valide o codigo enviado para o seu e-mail antes de entrar.", null));

        var jwtKey = builder.Configuration["Jwt:Key"];
        var jwtIssuer = builder.Configuration["Jwt:Issuer"];
        var jwtAudience = builder.Configuration["Jwt:Audience"];
        var jwtExpiryMinutes = builder.Configuration.GetValue<int>("Jwt:ExpiryMinutes", 60);
        
        if (string.IsNullOrEmpty(jwtKey)) throw new InvalidOperationException("JWT Key is missing in configuration.");
        if (string.IsNullOrEmpty(jwtIssuer)) throw new InvalidOperationException("JWT Issuer is missing in configuration.");
        if (string.IsNullOrEmpty(jwtAudience)) throw new InvalidOperationException("JWT Audience is missing in configuration.");
        
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(jwtKey!);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, usuario.Cpf),
            new(ClaimTypes.Name, usuario.Nome),
            new(ClaimTypes.Email, usuario.Email),
        };

        if (usuario.IsAdmin)
            claims.Add(new Claim(ClaimTypes.Role, "Admin"));

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(jwtExpiryMinutes),
            Issuer = jwtIssuer,
            Audience = jwtAudience,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };
        var token = tokenHandler.CreateToken(tokenDescriptor);
        var tokenString = tokenHandler.WriteToken(token);

        var response = new LoginResponse(tokenString, usuario.Cpf, usuario.Nome, usuario.Email, usuario.IsAdmin, tokenDescriptor.Expires!.Value);
        return Results.Ok(new LoginAttemptResponse(true, "Sessão iniciada com sucesso.", response));
    }
    catch (SqlException)
    {
        return Results.Problem(
            title: "Falha de banco de dados",
            detail: "Não foi possível conectar ao SQL Server.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapPost("/api/usuarios", async (UsuarioCreateDto usuario, IEmailVerificationSender emailVerificationSender, IOptions<EmailDeliveryOptions> emailOptions) =>
{
    var cpfLimpo = NormalizarCpf(usuario.Cpf);
    var erroCpf = ValidacoesEntrada.ObterErroCpf(cpfLimpo);
    if (erroCpf is not null)
        return Results.Ok(new ApiDataResponse<UsuarioCadastroResponse>(false, $"Erro: {erroCpf}", null));

    var nomeNormalizado = NormalizarNome(usuario.Nome);
    var erroNome = ValidacoesEntrada.ObterErroNomeCompleto(nomeNormalizado);
    if (erroNome is not null)
        return Results.Ok(new ApiDataResponse<UsuarioCadastroResponse>(false, $"Erro: {erroNome}", null));

    var emailNormalizado = NormalizarEmail(usuario.Email);
    var erroEmail = ValidacoesEntrada.ObterErroEmail(emailNormalizado);
    if (erroEmail is not null)
        return Results.Ok(new ApiDataResponse<UsuarioCadastroResponse>(false, $"Erro: {erroEmail}", null));

    var errosSenha = ValidacoesEntrada.ListarErrosSenha(usuario.Senha);
    if (errosSenha.Count > 0)
        return Results.Ok(new ApiDataResponse<UsuarioCadastroResponse>(false, $"Erro: {string.Join(' ', errosSenha)}", null));

    var codigoVerificacao = GerarCodigoVerificacao();
    var codigoVerificacaoHash = HashCodigoVerificacao(codigoVerificacao);
    var expiraEm = DateTime.UtcNow.AddMinutes(Math.Max(emailOptions.Value.VerificationCodeExpiryMinutes, 5));

    try
    {
        using var connection = new SqlConnection(connectionString);
        var checkSql = "SELECT COUNT(1) FROM Usuarios WHERE Cpf = @Cpf";
        var cpfExiste = await connection.ExecuteScalarAsync<int>(checkSql, new { Cpf = cpfLimpo });

        if (cpfExiste > 0)
        {
            return Results.Ok(new ApiDataResponse<UsuarioCadastroResponse>(false, "Erro: Este CPF já está cadastrado.", null));
        }

        var emailExiste = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Usuarios WHERE LOWER(Email) = @Email",
            new { Email = emailNormalizado });
        if (emailExiste > 0)
            return Results.Ok(new ApiDataResponse<UsuarioCadastroResponse>(false, "Erro: Este e-mail já está cadastrado.", null));

        var senhaHash = BCrypt.Net.BCrypt.HashPassword(usuario.Senha);
        var insertSql = @"INSERT INTO Usuarios (
                                Cpf,
                                Nome,
                                Email,
                                SenhaHash,
                                EmailVerificado,
                                CodigoVerificacaoHash,
                                CodigoVerificacaoExpiraEm,
                                TentativasCodigoEmail,
                                UltimoEnvioCodigoEm)
                           VALUES (
                                @Cpf,
                                @Nome,
                                @Email,
                                @SenhaHash,
                                0,
                                @CodigoVerificacaoHash,
                                @CodigoVerificacaoExpiraEm,
                                0,
                                @UltimoEnvioCodigoEm)";
        var novoUsuario = new
        {
            Cpf = cpfLimpo,
            Nome = nomeNormalizado,
            Email = emailNormalizado,
            SenhaHash = senhaHash,
            CodigoVerificacaoHash = codigoVerificacaoHash,
            CodigoVerificacaoExpiraEm = expiraEm,
            UltimoEnvioCodigoEm = DateTime.UtcNow,
        };
        await connection.ExecuteAsync(insertSql, novoUsuario);

        var envioCodigo = await emailVerificationSender.SendVerificationCodeAsync(nomeNormalizado, emailNormalizado, codigoVerificacao, expiraEm);

        var resposta = new UsuarioCadastroResponse(
            cpfLimpo,
            nomeNormalizado,
            emailNormalizado,
            false,
            expiraEm,
            envioCodigo.Success,
            envioCodigo.DeliveryMode,
            envioCodigo.UserMessage);

        return Results.Ok(new ApiDataResponse<UsuarioCadastroResponse>(true, "Conta criada com sucesso.", resposta));
    }
    catch (SqlException)
    {
        return Results.Problem(
            title: "Falha de banco de dados",
            detail: "Não foi possível conectar ao SQL Server. Verifique a connection string e se o banco está ativo.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapPost("/api/usuarios/verificar-email", async (UsuarioEmailVerificationRequest request) =>
{
    var cpfLimpo = NormalizarCpf(request.Cpf);
    if (!ValidacoesEntrada.CpfTem11Digitos(cpfLimpo))
        return Results.Ok(new UsuarioEmailVerificationResponse(false, "CPF invalido."));

    var codigo = (request.Codigo ?? string.Empty).Trim();
    if (!ValidacoesEntrada.CodigoVerificacaoValido(codigo))
        return Results.Ok(new UsuarioEmailVerificationResponse(false, "Codigo deve conter 6 digitos."));

    try
    {
        using var connection = new SqlConnection(connectionString);
        var usuario = await connection.QuerySingleOrDefaultAsync<UsuarioVerificacaoLookup>(
            @"SELECT Cpf, Nome, Email, EmailVerificado, CodigoVerificacaoHash, CodigoVerificacaoExpiraEm, TentativasCodigoEmail, UltimoEnvioCodigoEm
              FROM Usuarios
              WHERE Cpf = @Cpf",
            new { Cpf = cpfLimpo });

        if (usuario is null)
                        return Results.Ok(new UsuarioEmailVerificationResponse(false, "Usuario nao encontrado."));

        if (usuario.EmailVerificado)
            return Results.Ok(new UsuarioEmailVerificationResponse(true, "E-mail ja validado."));

        if (usuario.TentativasCodigoEmail >= MaxTentativasCodigoEmail)
            return Results.Ok(new UsuarioEmailVerificationResponse(false, "Numero maximo de tentativas atingido. Solicite um novo codigo."));

        if (string.IsNullOrWhiteSpace(usuario.CodigoVerificacaoHash) || usuario.CodigoVerificacaoExpiraEm is null)
            return Results.Ok(new UsuarioEmailVerificationResponse(false, "Nao existe codigo pendente para esta conta."));

        if (usuario.CodigoVerificacaoExpiraEm.Value < DateTime.UtcNow)
            return Results.Ok(new UsuarioEmailVerificationResponse(false, "Codigo expirado. Solicite um novo envio."));

        if (!string.Equals(usuario.CodigoVerificacaoHash, HashCodigoVerificacao(codigo), StringComparison.OrdinalIgnoreCase))
        {
            await connection.ExecuteAsync(
                @"UPDATE Usuarios
                  SET TentativasCodigoEmail = TentativasCodigoEmail + 1
                  WHERE Cpf = @Cpf",
                new { Cpf = cpfLimpo });

                        return Results.Ok(new UsuarioEmailVerificationResponse(false, "Codigo invalido."));
        }

        await connection.ExecuteAsync(
            @"UPDATE Usuarios
              SET EmailVerificado = 1,
                  EmailVerificadoEm = GETUTCDATE(),
                  CodigoVerificacaoHash = NULL,
                  CodigoVerificacaoExpiraEm = NULL,
                  TentativasCodigoEmail = 0,
                  UltimoEnvioCodigoEm = NULL
              WHERE Cpf = @Cpf",
            new { Cpf = cpfLimpo });

        return Results.Ok(new UsuarioEmailVerificationResponse(true, "E-mail validado com sucesso."));
    }
    catch (SqlException)
    {
        return Results.Problem(
            title: "Falha de banco de dados",
            detail: "Nao foi possivel validar o codigo de e-mail.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapPost("/api/usuarios/reenviar-codigo", async (UsuarioCodigoReenvioRequest request, IEmailVerificationSender emailVerificationSender, IOptions<EmailDeliveryOptions> emailOptions) =>
{
    var cpfLimpo = NormalizarCpf(request.Cpf);
    if (!ValidacoesEntrada.CpfTem11Digitos(cpfLimpo))
        return Results.Ok(new EmailVerificationDeliveryResponse(false, "CPF invalido.", false, DateTime.UtcNow, "nenhum", "CPF invalido."));

    try
    {
        using var connection = new SqlConnection(connectionString);
        var usuario = await connection.QuerySingleOrDefaultAsync<UsuarioVerificacaoLookup>(
            @"SELECT Cpf, Nome, Email, EmailVerificado, CodigoVerificacaoHash, CodigoVerificacaoExpiraEm, TentativasCodigoEmail, UltimoEnvioCodigoEm
              FROM Usuarios
              WHERE Cpf = @Cpf",
            new { Cpf = cpfLimpo });

        if (usuario is null)
            return Results.Ok(new EmailVerificationDeliveryResponse(false, "Usuario nao encontrado.", false, DateTime.UtcNow, "nenhum", "Usuario nao encontrado."));

        if (usuario.EmailVerificado)
            return Results.Ok(new EmailVerificationDeliveryResponse(false, "Esta conta ja foi validada.", false, DateTime.UtcNow, "nenhum", "Esta conta ja foi validada."));

        if (usuario.UltimoEnvioCodigoEm is not null && usuario.UltimoEnvioCodigoEm.Value.AddSeconds(IntervaloMinimoReenvioCodigoSegundos) > DateTime.UtcNow)
            return Results.Ok(new EmailVerificationDeliveryResponse(false, $"Aguarde {IntervaloMinimoReenvioCodigoSegundos} segundos antes de reenviar o codigo.", false, usuario.CodigoVerificacaoExpiraEm ?? DateTime.UtcNow, "nenhum", $"Aguarde {IntervaloMinimoReenvioCodigoSegundos} segundos antes de reenviar o codigo."));

        var codigoVerificacao = GerarCodigoVerificacao();
        var codigoVerificacaoHash = HashCodigoVerificacao(codigoVerificacao);
        var expiraEm = DateTime.UtcNow.AddMinutes(Math.Max(emailOptions.Value.VerificationCodeExpiryMinutes, 5));

        await connection.ExecuteAsync(
            @"UPDATE Usuarios
              SET CodigoVerificacaoHash = @CodigoVerificacaoHash,
                  CodigoVerificacaoExpiraEm = @CodigoVerificacaoExpiraEm,
                  TentativasCodigoEmail = 0,
                  UltimoEnvioCodigoEm = @UltimoEnvioCodigoEm
              WHERE Cpf = @Cpf",
            new
            {
                Cpf = cpfLimpo,
                CodigoVerificacaoHash = codigoVerificacaoHash,
                CodigoVerificacaoExpiraEm = expiraEm,
                UltimoEnvioCodigoEm = DateTime.UtcNow,
            });

        var envioCodigo = await emailVerificationSender.SendVerificationCodeAsync(usuario.Nome, usuario.Email, codigoVerificacao, expiraEm);

        return Results.Ok(new EmailVerificationDeliveryResponse(true, envioCodigo.UserMessage, envioCodigo.Success, expiraEm, envioCodigo.DeliveryMode, envioCodigo.UserMessage));
    }
    catch (SqlException)
    {
        return Results.Problem(
            title: "Falha de banco de dados",
            detail: "Nao foi possivel reenviar o codigo de e-mail.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

// ================================
// ENDPOINTS DE ASSENTOS
// ================================
app.MapGet("/api/eventos/{eventId}/assentos", async (int eventId) =>
{
    try
    {
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await GarantirMapaPadraoAssentosParaEventoAsync(connection, eventId);
        await LiberarReservasPendentesExpiradasAsync(connection, eventId);

        var sql = "SELECT Id, EventoId, Fila, Numero, Tipo, PrecoAdicional, Status, LockedUntil, LockedByCpf FROM Assentos WHERE EventoId = @EventoId ORDER BY Fila, Numero";
        var assentos = await connection.QueryAsync<AssentoDto>(sql, new { EventoId = eventId });
        return Results.Ok(assentos);
    }
    catch (SqlException)
    {
        return Results.Problem(
            title: "Falha de banco de dados",
            detail: "Não foi possível conectar ao SQL Server.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapPost("/api/assentos/{seatId}/lock", async (int seatId, AssentoLockRequest request, HttpContext httpContext) =>
{
    var cpf = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrEmpty(cpf))
        return Results.Unauthorized();

    if (request.EventoId <= 0 || request.SeatId != seatId)
        return Results.Ok(new AssentoLockResponse(false, "Dados inválidos.", null));

    try
    {
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await GarantirMapaPadraoAssentosParaEventoAsync(connection, request.EventoId);
        await LiberarReservasPendentesExpiradasAsync(connection, request.EventoId);

        var now = DateTime.UtcNow;
        var seat = await connection.QuerySingleOrDefaultAsync<AssentoDisponibilidadeLookup>(
            "SELECT Id, EventoId, Status, LockedUntil, LockedByCpf FROM Assentos WHERE Id = @Id AND EventoId = @EventoId",
            new { Id = seatId, EventoId = request.EventoId });
        if (seat == null)
            return Results.Ok(new AssentoLockResponse(false, "Assento não encontrado.", null));

        if (seat.Status == "reservado" && seat.LockedByCpf == cpf && seat.LockedUntil is not null && seat.LockedUntil >= now)
            return Results.Ok(new AssentoLockResponse(true, "Assento já está bloqueado por você.", seat.LockedUntil));

        var lockAindaValido = seat.LockedUntil == null || seat.LockedUntil >= now;
        if (seat.Status != "disponivel" && lockAindaValido)
            return Results.Ok(new AssentoLockResponse(false, "Assento não está disponível.", seat.LockedUntil));

        var lockedUntil = now.AddMinutes(5);
        var updateSql = @"UPDATE Assentos SET Status = 'reservado', LockedUntil = @LockedUntil, LockedByCpf = @LockedByCpf
                          WHERE Id = @Id AND (Status = 'disponivel' OR (LockedUntil IS NOT NULL AND LockedUntil < @Now))";
        var affected = await connection.ExecuteAsync(updateSql, new
        {
            Id = seatId,
            LockedUntil = lockedUntil,
            LockedByCpf = cpf,
            Now = now
        });
        if (affected == 0)
            return Results.Ok(new AssentoLockResponse(false, "Não foi possível bloquear o assento. Pode já estar ocupado.", null));

        // Notify via SignalR (optional)
        return Results.Ok(new AssentoLockResponse(true, "Assento bloqueado com sucesso.", lockedUntil));
    }
    catch (SqlException)
    {
        return Results.Problem(
            title: "Falha de banco de dados",
            detail: "Não foi possível conectar ao SQL Server.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}).RequireAuthorization();

app.MapPost("/api/assentos/{seatId}/release", async (int seatId, HttpContext httpContext) =>
{
    var cpf = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrEmpty(cpf))
        return Results.Unauthorized();

    try
    {
        using var connection = new SqlConnection(connectionString);
        var updateSql = @"UPDATE Assentos SET Status = 'disponivel', LockedUntil = NULL, LockedByCpf = NULL
                          WHERE Id = @Id AND LockedByCpf = @LockedByCpf";
        var affected = await connection.ExecuteAsync(updateSql, new
        {
            Id = seatId,
            LockedByCpf = cpf
        });
        if (affected == 0)
            return Results.Ok(new ApiOperationResponse(false, "Assento não está bloqueado por você ou já foi liberado."));

        return Results.Ok(new ApiOperationResponse(true, "Assento liberado."));
    }
    catch (SqlException)
    {
        return Results.Problem(
            title: "Falha de banco de dados",
            detail: "Não foi possível conectar ao SQL Server.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}).RequireAuthorization();

// ================================
// ENDPOINTS DE FILA
// ================================
app.MapPost("/api/fila/join", async (FilaRequest request, HttpContext httpContext) =>
{
    var cpf = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrEmpty(cpf))
        return Results.Unauthorized();

    try
    {
        using var connection = new SqlConnection(connectionString);
        var existing = await connection.QuerySingleOrDefaultAsync<FilaDto>(
            "SELECT Id, EventoId, UsuarioCpf, Posicao, Status, DataEntrada, TempoEstimado FROM FilaEvento WHERE EventoId = @EventoId AND UsuarioCpf = @UsuarioCpf AND Status = 'espera'",
            new { EventoId = request.EventoId, UsuarioCpf = cpf });
        if (existing != null)
            return Results.Ok(new ApiDataResponse<FilaResponse>(false, "Você já está na fila para este evento.", null));

        var maxPos = await connection.ExecuteScalarAsync<int?>(
            "SELECT MAX(Posicao) FROM FilaEvento WHERE EventoId = @EventoId AND Status = 'espera'",
            new { EventoId = request.EventoId }) ?? 0;
        var newPos = maxPos + 1;

        var insertSql = @"INSERT INTO FilaEvento (EventoId, UsuarioCpf, Posicao, Status, DataEntrada)
                          VALUES (@EventoId, @UsuarioCpf, @Posicao, 'espera', GETUTCDATE())";
        await connection.ExecuteAsync(insertSql, new
        {
            EventoId = request.EventoId,
            UsuarioCpf = cpf,
            Posicao = newPos
        });

        var fila = new FilaResponse(request.EventoId, cpf, newPos, "espera", DateTime.UtcNow, null);
        return Results.Ok(new ApiDataResponse<FilaResponse>(true, "Você entrou na fila com sucesso.", fila));
    }
    catch (SqlException)
    {
        return Results.Problem(
            title: "Falha de banco de dados",
            detail: "Não foi possível conectar ao SQL Server.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}).RequireAuthorization();

app.MapPost("/api/fila/leave", async (FilaRequest request, HttpContext httpContext) =>
{
    var cpf = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrEmpty(cpf))
        return Results.Unauthorized();

    try
    {
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();

        var filaAtual = await connection.QuerySingleOrDefaultAsync<FilaDto>(
            "SELECT Id, EventoId, UsuarioCpf, Posicao, Status, DataEntrada, TempoEstimado FROM FilaEvento WHERE EventoId = @EventoId AND UsuarioCpf = @UsuarioCpf AND Status = 'espera'",
            new { EventoId = request.EventoId, UsuarioCpf = cpf },
            transaction);
        if (filaAtual == null)
        {
            transaction.Rollback();
            return Results.Ok(new ApiOperationResponse(false, "Você não está na fila para este evento."));
        }

        var deleteSql = @"DELETE FROM FilaEvento WHERE Id = @Id";
        await connection.ExecuteAsync(deleteSql, new { filaAtual.Id }, transaction);

        await connection.ExecuteAsync(
            @"UPDATE FilaEvento
              SET Posicao = Posicao - 1
              WHERE EventoId = @EventoId AND Status = 'espera' AND Posicao > @Posicao",
            new { EventoId = request.EventoId, filaAtual.Posicao },
            transaction);

        transaction.Commit();

        return Results.Ok(new ApiOperationResponse(true, "Você saiu da fila."));
    }
    catch (SqlException)
    {
        return Results.Problem(
            title: "Falha de banco de dados",
            detail: "Não foi possível conectar ao SQL Server.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}).RequireAuthorization();

app.MapGet("/api/fila/{eventoId}/position", async (int eventoId, HttpContext httpContext) =>
{
    var cpf = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrEmpty(cpf))
        return Results.Unauthorized();

    try
    {
        using var connection = new SqlConnection(connectionString);
        var fila = await connection.QuerySingleOrDefaultAsync<FilaDto>(
            "SELECT Id, EventoId, UsuarioCpf, Posicao, Status, DataEntrada, TempoEstimado FROM FilaEvento WHERE EventoId = @EventoId AND UsuarioCpf = @UsuarioCpf AND Status = 'espera'",
            new { EventoId = eventoId, UsuarioCpf = cpf });
        if (fila == null)
            return Results.NoContent();

        return Results.Ok(new FilaResponse(fila.EventoId, fila.UsuarioCpf, fila.Posicao, fila.Status, fila.DataEntrada, fila.TempoEstimado));
    }
    catch (SqlException)
    {
        return Results.Problem(
            title: "Falha de banco de dados",
            detail: "Não foi possível conectar ao SQL Server.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}).RequireAuthorization();

app.MapGet("/api/reservas/me", async (HttpContext httpContext) =>
{
    var cpf = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrEmpty(cpf))
        return Results.Unauthorized();

    try
    {
        using var connection = new SqlConnection(connectionString);
        var sql = @"
            SELECT
                r.Id,
                r.UsuarioCpf,
                r.EventoId,
                e.Nome AS EventoNome,
                r.CupomUtilizado,
                r.ValorFinalPago,
                r.Status,
                ticketInfo.AssentosResumo
            FROM Reservas r
            INNER JOIN Eventos e ON r.EventoId = e.Id
            OUTER APPLY (
                SELECT STRING_AGG(CONCAT(a.Fila, a.Numero), ', ') AS AssentosResumo
                FROM Tickets t
                INNER JOIN Assentos a ON a.Id = t.AssentoId
                WHERE t.ReservaId = r.Id
                  AND t.Status != 'cancelado'
            ) ticketInfo
            WHERE r.UsuarioCpf = @Cpf
            ORDER BY r.Id";
        var reservas = await connection.QueryAsync<ReservaDto>(sql, new { Cpf = cpf });
        return Results.Ok(reservas);
    }
    catch (SqlException)
    {
        return Results.Problem(
            title: "Falha de banco de dados",
            detail: "Não foi possível conectar ao SQL Server.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}).RequireAuthorization();

app.MapGet("/api/reservas/{cpf}", async (string cpf, HttpContext httpContext) =>
{
    var cpfSolicitante = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrEmpty(cpfSolicitante))
        return Results.Unauthorized();

    var cpfLimpo = new string((cpf ?? string.Empty).Where(char.IsDigit).ToArray());
    if (!ValidacoesEntrada.CpfTem11Digitos(cpfLimpo))
        return Results.Ok(new ApiDataResponse<IReadOnlyList<ReservaDto>>(false, "CPF inválido.", null));

    try
    {
        using var connection = new SqlConnection(connectionString);
        if (!string.Equals(cpfSolicitante, cpfLimpo, StringComparison.Ordinal) && !await UsuarioEhAdminAsync(connection, cpfSolicitante))
            return Results.Forbid();

        var sql = @"
            SELECT
                r.Id,
                r.UsuarioCpf,
                r.EventoId,
                e.Nome AS EventoNome,
                r.CupomUtilizado,
                r.ValorFinalPago,
                r.Status,
                ticketInfo.AssentosResumo
            FROM Reservas r
            INNER JOIN Eventos e ON r.EventoId = e.Id
            OUTER APPLY (
                SELECT STRING_AGG(CONCAT(a.Fila, a.Numero), ', ') AS AssentosResumo
                FROM Tickets t
                INNER JOIN Assentos a ON a.Id = t.AssentoId
                WHERE t.ReservaId = r.Id
                  AND t.Status != 'cancelado'
            ) ticketInfo
            WHERE r.UsuarioCpf = @Cpf
            ORDER BY r.Id";
        var reservas = (await connection.QueryAsync<ReservaDto>(sql, new { Cpf = cpfLimpo })).ToArray();
        return Results.Ok(new ApiDataResponse<IReadOnlyList<ReservaDto>>(true, "Reservas consultadas com sucesso.", reservas));
    }
    catch (SqlException)
    {
        return Results.Problem(
            title: "Falha de banco de dados",
            detail: "Não foi possível conectar ao SQL Server.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}).RequireAuthorization();

app.MapPost("/api/reservas", async (ReservaCreateDto reserva, HttpContext httpContext) =>
{
    var cpf = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrEmpty(cpf))
        return Results.Unauthorized();

    var cpfLimpo = NormalizarCpf(cpf);
    var cpfReserva = NormalizarCpf(reserva.UsuarioCpf);
    if (!string.IsNullOrWhiteSpace(reserva.UsuarioCpf) && !string.Equals(cpfReserva, cpfLimpo, StringComparison.Ordinal))
        return Results.Ok(new ApiDataResponse<ReservaDto>(false, "O CPF da reserva deve corresponder ao usuário autenticado.", null));

    if (!ValidacoesEntrada.CpfTem11Digitos(cpfLimpo))
        return Results.Ok(new ApiDataResponse<ReservaDto>(false, "CPF inválido.", null));

    var assentoIds = (reserva.AssentoIds ?? [])
        .Where(assentoId => assentoId > 0)
        .Distinct()
        .ToArray();

    try
    {
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();

        await GarantirMapaPadraoAssentosParaEventoAsync(connection, reserva.EventoId, transaction);
        await LiberarReservasPendentesExpiradasAsync(connection, reserva.EventoId, transaction);

        var usuarioExiste = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Usuarios WHERE Cpf = @Cpf",
            new { Cpf = cpfLimpo },
            transaction);
        if (usuarioExiste == 0)
            return Results.Ok(new ApiDataResponse<ReservaDto>(false, "Usuário não encontrado.", null));

        var evento = await connection.QuerySingleOrDefaultAsync<EventoReservaLookup>(
            @"SELECT Nome, CapacidadeTotal, PrecoPadrao, ISNULL(TaxaFixa, 5.00) AS TaxaFixa, Status
              FROM Eventos
              WHERE Id = @EventoId",
            new { EventoId = reserva.EventoId },
            transaction);
        if (evento == null)
            return Results.Ok(new ApiDataResponse<ReservaDto>(false, "Evento não encontrado.", null));

        if (!string.Equals(evento.Status, "ativo", StringComparison.OrdinalIgnoreCase))
            return Results.Ok(new ApiDataResponse<ReservaDto>(false, "Evento não está disponível para reserva.", null));

        var reservasExistentes = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Reservas WHERE UsuarioCpf = @Cpf AND EventoId = @EventoId AND Status != 'cancelada'",
            new { Cpf = cpfLimpo, reserva.EventoId },
            transaction);
        if (reservasExistentes >= 2)
            return Results.Ok(new ApiDataResponse<ReservaDto>(false, "Limite de 2 reservas por CPF para este evento já atingido.", null));

        AssentoReservaSelecionado[] assentosSelecionados = [];
        if (assentoIds.Length > 0)
        {
            assentosSelecionados = (await connection.QueryAsync<AssentoReservaSelecionado>(
                @"SELECT Id, EventoId, Fila, Numero, Tipo, PrecoAdicional, Status, LockedUntil, LockedByCpf
                  FROM Assentos
                  WHERE EventoId = @EventoId
                    AND Id IN @Ids",
                new { EventoId = reserva.EventoId, Ids = assentoIds },
                transaction))
                .ToArray();

            if (assentosSelecionados.Length != assentoIds.Length)
                return Results.Ok(new ApiDataResponse<ReservaDto>(false, "Alguns assentos selecionados não pertencem ao evento informado.", null));

            if (assentosSelecionados.Any(assento =>
                    !string.Equals(assento.Status, "reservado", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(assento.LockedByCpf, cpfLimpo, StringComparison.Ordinal) ||
                    assento.LockedUntil is null ||
                    assento.LockedUntil.Value < DateTime.UtcNow))
            {
                return Results.Ok(new ApiDataResponse<ReservaDto>(false, "Selecione e bloqueie seus assentos antes de confirmar a reserva.", null));
            }
        }
        else
        {
            var reservasTotais = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM Reservas WHERE EventoId = @EventoId AND Status != 'cancelada'",
                new { reserva.EventoId },
                transaction);
            if (reservasTotais >= evento.CapacidadeTotal)
                return Results.Ok(new ApiDataResponse<ReservaDto>(false, "Capacidade do evento esgotada.", null));
        }

        CupomReservaLookup? cupom = null;
        var cupomCodigo = reserva.CupomUtilizado?.Trim().ToUpperInvariant();
        if (!string.IsNullOrWhiteSpace(reserva.CupomUtilizado))
        {
            cupom = await connection.QuerySingleOrDefaultAsync<CupomReservaLookup>(
                @"SELECT Codigo, PorcentagemDesconto, ValorMinimoRegra, DataExpiracao
                  FROM Cupons
                  WHERE Codigo = @Codigo
                    AND (DataExpiracao IS NULL OR DataExpiracao >= GETDATE())",
                new { Codigo = cupomCodigo },
                transaction);
            if (cupom == null)
                return Results.Ok(new ApiDataResponse<ReservaDto>(false, "Cupom inválido.", null));
        }

        var subtotalBase = assentosSelecionados.Length > 0
            ? assentosSelecionados.Sum(assento => evento.PrecoPadrao + assento.PrecoAdicional)
            : evento.PrecoPadrao;

        var valorFinal = PriceCalculator.CalculateFinalPrice(
            subtotalBase,
            cupom?.PorcentagemDesconto ?? 0,
            cupom?.ValorMinimoRegra ?? 0,
            evento.TaxaFixa,
            cupom is not null);

        var insertSql = @"
            INSERT INTO Reservas (UsuarioCpf, EventoId, CupomUtilizado, ValorFinalPago)
            VALUES (@UsuarioCpf, @EventoId, @CupomUtilizado, @ValorFinalPago);
            SELECT CAST(SCOPE_IDENTITY() AS INT)";
        var id = await connection.ExecuteScalarAsync<int>(insertSql, new
        {
            UsuarioCpf = cpfLimpo,
            reserva.EventoId,
            CupomUtilizado = cupomCodigo,
            ValorFinalPago = valorFinal
        }, transaction);

        string? assentosResumo = null;
        if (assentosSelecionados.Length > 0)
        {
            var valoresTickets = DistribuirValorTickets(assentosSelecionados.Length, valorFinal);
            for (var index = 0; index < assentosSelecionados.Length; index++)
            {
                await connection.ExecuteAsync(
                    @"INSERT INTO Tickets (ReservaId, AssentoId, PrecoPago)
                      VALUES (@ReservaId, @AssentoId, @PrecoPago)",
                    new
                    {
                        ReservaId = id,
                        AssentoId = assentosSelecionados[index].Id,
                        PrecoPago = valoresTickets[index],
                    },
                    transaction);
            }

            await connection.ExecuteAsync(
                @"UPDATE Assentos
                  SET Status = 'reservado',
                      LockedUntil = DATEADD(minute, @MinutosExpiracao, GETUTCDATE()),
                      LockedByCpf = @Cpf
                  WHERE Id IN @Ids",
                new
                {
                    Ids = assentoIds,
                    MinutosExpiracao = MinutosExpiracaoReservaPendente,
                    Cpf = cpfLimpo,
                },
                transaction);

            assentosResumo = MontarResumoAssentos(assentosSelecionados);
        }

        transaction.Commit();

        var reservaCriada = new ReservaDto(id, cpfLimpo, reserva.EventoId, evento.Nome, cupomCodigo, valorFinal, "pendente", assentosResumo);
        return Results.Ok(new ApiDataResponse<ReservaDto>(true, "Reserva criada com sucesso.", reservaCriada));
    }
    catch (SqlException)
    {
        return Results.Problem(
            title: "Falha de banco de dados",
            detail: "Não foi possível conectar ao SQL Server.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}).RequireAuthorization();


app.MapPost("/api/reservas/{reservaId}/pagamentos", async (int reservaId, PagamentoCreateDto pagamento, HttpContext httpContext) =>
{
    var cpf = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrEmpty(cpf))
        return Results.Unauthorized();

    try
    {
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        var reservaBase = await connection.QuerySingleOrDefaultAsync<ReservaPagamentoLookup>(
            "SELECT Id, UsuarioCpf, EventoId, ValorFinalPago, Status FROM Reservas WHERE Id = @Id AND UsuarioCpf = @UsuarioCpf",
            new { Id = reservaId, UsuarioCpf = cpf });
        if (reservaBase == null)
            return Results.Ok(new ApiDataResponse<PagamentoDto>(false, "Reserva não encontrada ou não pertence ao usuário.", null));

        await LiberarReservasPendentesExpiradasAsync(connection, reservaBase.EventoId);

        var reserva = await connection.QuerySingleOrDefaultAsync<ReservaPagamentoLookup>(
            "SELECT Id, UsuarioCpf, EventoId, ValorFinalPago, Status FROM Reservas WHERE Id = @Id AND UsuarioCpf = @UsuarioCpf",
            new { Id = reservaId, UsuarioCpf = cpf });
        if (reserva == null)
            return Results.Ok(new ApiDataResponse<PagamentoDto>(false, "Reserva não encontrada ou não pertence ao usuário.", null));

        if (string.Equals(reserva.Status, "cancelada", StringComparison.OrdinalIgnoreCase))
            return Results.Ok(new ApiDataResponse<PagamentoDto>(false, "A seleção de assentos expirou ou a reserva foi cancelada. Refaça a operação.", null));

        if (string.Equals(reserva.Status, "confirmada", StringComparison.OrdinalIgnoreCase))
            return Results.Ok(new ApiDataResponse<PagamentoDto>(false, "Esta reserva já está confirmada.", null));

        if (pagamento.Metodo != "pix" && pagamento.Metodo != "cartao_credito" && pagamento.Metodo != "cartao_debito")
            return Results.Ok(new ApiDataResponse<PagamentoDto>(false, "Método de pagamento inválido.", null));

        var assentosReserva = (await connection.QueryAsync<ReservaPagamentoAssentoLookup>(
            @"SELECT t.AssentoId, a.Status, a.LockedUntil, a.LockedByCpf
              FROM Tickets t
              INNER JOIN Assentos a ON a.Id = t.AssentoId
              WHERE t.ReservaId = @ReservaId
                AND t.Status != 'cancelado'",
            new { ReservaId = reservaId }))
            .ToArray();

        if (assentosReserva.Length > 0 && assentosReserva.Any(assento =>
                !string.Equals(assento.Status, "reservado", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(assento.LockedByCpf, cpf, StringComparison.Ordinal) ||
                assento.LockedUntil is null ||
                assento.LockedUntil.Value < DateTime.UtcNow))
        {
            return Results.Ok(new ApiDataResponse<PagamentoDto>(false, "A seleção de assentos expirou. Escolha os assentos novamente.", null));
        }

        bool sucesso = Random.Shared.NextDouble() > 0.3;
        string status = sucesso ? "aprovado" : "recusado";
        string? transacaoId = sucesso ? $"TX-{DateTime.UtcNow.Ticks}" : null;

        using var transaction = connection.BeginTransaction();

        var insertSql = @"INSERT INTO Pagamentos (ReservaId, Metodo, ValorTotal, Status, TransacaoId, Parcelas, DataPagamento, DataAtualizacao)
                          VALUES (@ReservaId, @Metodo, @ValorTotal, @Status, @TransacaoId, @Parcelas, GETDATE(), GETDATE());
                          SELECT CAST(SCOPE_IDENTITY() AS INT)";
        var pagamentoId = await connection.ExecuteScalarAsync<int>(insertSql, new
        {
            ReservaId = reservaId,
            Metodo = pagamento.Metodo,
            ValorTotal = reserva.ValorFinalPago,
            Status = status,
            TransacaoId = transacaoId,
            Parcelas = pagamento.Parcelas
        }, transaction);

        if (sucesso)
        {
            await connection.ExecuteAsync(
                "UPDATE Reservas SET Status = 'confirmada', PagamentoId = @PagamentoId WHERE Id = @Id",
                new { Id = reservaId, PagamentoId = pagamentoId },
                transaction);

            if (assentosReserva.Length > 0)
            {
                await connection.ExecuteAsync(
                    @"UPDATE Assentos
                      SET Status = 'ocupado',
                          LockedUntil = NULL,
                          LockedByCpf = NULL
                      WHERE Id IN @Ids",
                    new { Ids = assentosReserva.Select(assento => assento.AssentoId).Distinct().ToArray() },
                    transaction);
            }
        }

        transaction.Commit();

        var pagamentoDto = new PagamentoDto(pagamentoId, reservaId, pagamento.Metodo, reserva.ValorFinalPago, status, transacaoId, pagamento.Parcelas, DateTime.UtcNow, DateTime.UtcNow);
        return Results.Ok(new ApiDataResponse<PagamentoDto>(true, "Pagamento processado com sucesso.", pagamentoDto));
    }
    catch (SqlException)
    {
        return Results.Problem(
            title: "Falha de banco de dados",
            detail: "Não foi possível conectar ao SQL Server.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}).RequireAuthorization();

app.MapPost("/api/reservas/{reservaId}/cancelar", async (int reservaId, HttpContext httpContext) =>
{
    var cpf = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrEmpty(cpf))
        return Results.Unauthorized();

    try
    {
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();

        var reserva = await connection.QuerySingleOrDefaultAsync<ReservaStatusLookup>(
            "SELECT Id, UsuarioCpf, EventoId, ValorFinalPago, Status FROM Reservas WHERE Id = @Id AND UsuarioCpf = @UsuarioCpf",
            new { Id = reservaId, UsuarioCpf = cpf },
            transaction);
        if (reserva == null)
            return Results.Ok(new ApiOperationResponse(false, "Reserva não encontrada ou não pertence ao usuário."));

        if (reserva.Status == "cancelada")
            return Results.Ok(new ApiOperationResponse(false, "Reserva já está cancelada."));

        await connection.ExecuteAsync(
            "UPDATE Reservas SET Status = 'cancelada' WHERE Id = @Id",
            new { Id = reservaId },
            transaction);

        var assentoIds = (await connection.QueryAsync<int>(
            "SELECT AssentoId FROM Tickets WHERE ReservaId = @ReservaId",
            new { ReservaId = reservaId },
            transaction))
            .Distinct()
            .ToArray();

        await connection.ExecuteAsync(
            "UPDATE Tickets SET Status = 'cancelado' WHERE ReservaId = @ReservaId",
            new { ReservaId = reservaId },
            transaction);

        if (assentoIds.Length > 0)
        {
            await connection.ExecuteAsync(
                @"UPDATE Assentos
                  SET Status = 'disponivel',
                      LockedUntil = NULL,
                      LockedByCpf = NULL
                  WHERE Id IN @Ids",
                new { Ids = assentoIds },
                transaction);
        }

        var pagamento = await connection.QuerySingleOrDefaultAsync<PagamentoStatusLookup>(
            "SELECT Id, Status FROM Pagamentos WHERE ReservaId = @ReservaId",
            new { ReservaId = reservaId },
            transaction);
        if (pagamento != null && pagamento.Status == "aprovado")
        {
            await connection.ExecuteAsync(
                "UPDATE Pagamentos SET Status = 'estornado', DataAtualizacao = GETDATE() WHERE Id = @Id",
                new { Id = pagamento.Id },
                transaction);
        }

        var cupomCodigo = $"CANCEL-{reservaId}-{DateTime.UtcNow:yyyyMMddHHmm}";
        var cupomInsert = @"INSERT INTO Cupons (Codigo, PorcentagemDesconto, ValorMinimoRegra, DataExpiracao)
                            VALUES (@Codigo, 10.00, 0.00, DATEADD(month, 3, GETDATE()))";
        await connection.ExecuteAsync(cupomInsert, new { Codigo = cupomCodigo }, transaction);

        transaction.Commit();

        return Results.Ok(new ApiOperationResponse(true, $"Reserva cancelada com sucesso. Um cupom de 10% de desconto foi gerado para você: {cupomCodigo}."));
    }
    catch (SqlException)
    {
        return Results.Problem(
            title: "Falha de banco de dados",
            detail: "Não foi possível conectar ao SQL Server.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}).RequireAuthorization();

app.MapHealthChecks("/health");
app.MapFallbackToFile("index.html");

app.Run();

// ==========================================
// MODELOS DE DADOS
// ==========================================
public record EventoDto(int Id, string Nome, int CapacidadeTotal, DateTime DataEvento, decimal PrecoPadrao, string? TipoEvento, string? Descricao, string? LocalNome, string? LocalCidade, string? BannerUrl, string? GaleriaTexto, decimal? TaxaFixa, string Status);
public record EventoCreateDto(string Nome, int CapacidadeTotal, DateTime DataEvento, decimal PrecoPadrao, string? TipoEvento, string? Descricao, string? LocalNome, string? LocalCidade, string? BannerUrl, string? GaleriaTexto, decimal? TaxaFixa, string? Status);
public record SetorDto(int Id, int EventoId, string Nome, decimal Preco, string? Cor, int? Capacidade, int Ordem);
public record SetorCreateDto(string Nome, decimal Preco, string? Cor, int? Capacidade, int Ordem);
public record CupomCreateDto(string Codigo, decimal PorcentagemDesconto, decimal ValorMinimoRegra);
public record UsuarioDto(string Cpf, string Nome, string Email);
public record UsuarioCreateDto(string Cpf, string Nome, string Email, string Senha);
public record UsuarioCadastroResponse(string Cpf, string Nome, string Email, bool EmailVerificado, DateTime CodigoExpiraEm, bool CodigoEnviado, string MetodoEntrega, string MensagemEntrega);
public record UsuarioLoginDb(string Cpf, string Nome, string Email, string SenhaHash, bool IsAdmin, bool EmailVerificado);
public record UsuarioEmailVerificationRequest(string Cpf, string Codigo);
public record UsuarioEmailVerificationResponse(bool Success, string Message);
public record UsuarioCodigoReenvioRequest(string Cpf);
public record ApiOperationResponse(bool Success, string Message);
public record ApiDataResponse<T>(bool Success, string Message, T? Data);
public record EmailVerificationDeliveryResponse(bool Success, string Message, bool CodigoEnviado, DateTime CodigoExpiraEm, string MetodoEntrega, string MensagemEntrega);
public record LoginRequest(string Cpf, string Senha);
public record LoginResponse(string Token, string Cpf, string Nome, string Email, bool IsAdmin, DateTime ExpiresAt);
public record LoginAttemptResponse(bool Success, string Message, LoginResponse? Session);
public record AssentoDto(int Id, int EventoId, string Fila, string Numero, string Tipo, decimal PrecoAdicional, string Status, DateTime? LockedUntil, string? LockedByCpf);
public record AssentoCreateDto(string Fila, string Numero, string Tipo, decimal PrecoAdicional);
public record AssentoLockRequest(int SeatId, int EventoId);
public record AssentoLockResponse(bool Success, string? Message, DateTime? LockedUntil);
public record FilaRequest(int EventoId);
public record FilaResponse(int EventoId, string UsuarioCpf, int Posicao, string Status, DateTime DataEntrada, int? TempoEstimado);
public record FilaDto(int Id, int EventoId, string UsuarioCpf, int Posicao, string Status, DateTime DataEntrada, int? TempoEstimado);
public record ReservaDto(int Id, string UsuarioCpf, int EventoId, string EventoNome, string? CupomUtilizado, decimal ValorFinalPago, string Status, string? AssentosResumo);
public record ReservaCreateDto(string UsuarioCpf, int EventoId, string? CupomUtilizado, IReadOnlyList<int>? AssentoIds);
public record PagamentoDto(int Id, int ReservaId, string Metodo, decimal ValorTotal, string Status, string? TransacaoId, int? Parcelas, DateTime? DataPagamento, DateTime DataAtualizacao);
public record PagamentoCreateDto(string Metodo, int? Parcelas);
public record EventoMapaAssentosLookup(int Id, int CapacidadeTotal, decimal PrecoPadrao);
public record EventoPrecoLookup(int Id, decimal PrecoPadrao);
public record EventoStatusLookup(int Id, string Status);
public record EventoReservaLookup(string Nome, int CapacidadeTotal, decimal PrecoPadrao, decimal TaxaFixa, string Status);
public record CupomReservaLookup(string Codigo, decimal PorcentagemDesconto, decimal ValorMinimoRegra, DateTime? DataExpiracao);
public record AssentoDisponibilidadeLookup(int Id, int EventoId, string Status, DateTime? LockedUntil, string? LockedByCpf);
public record AssentoReservaSelecionado(int Id, int EventoId, string Fila, string Numero, string Tipo, decimal PrecoAdicional, string Status, DateTime? LockedUntil, string? LockedByCpf);
public record ReservaPagamentoLookup(int Id, string UsuarioCpf, int EventoId, decimal ValorFinalPago, string Status);
public record ReservaPagamentoAssentoLookup(int AssentoId, string Status, DateTime? LockedUntil, string? LockedByCpf);
public record ReservaStatusLookup(int Id, string UsuarioCpf, int EventoId, decimal ValorFinalPago, string Status);
public record PagamentoStatusLookup(int Id, string Status);
public record UsuarioVerificacaoLookup(string Cpf, string Nome, string Email, bool EmailVerificado, string? CodigoVerificacaoHash, DateTime? CodigoVerificacaoExpiraEm, int TentativasCodigoEmail, DateTime? UltimoEnvioCodigoEm);

public partial class Program;
