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
using System.IdentityModel.Tokens.Jwt;
using BCrypt.Net;
using TicketPrime.Server.Hubs;
using Serilog;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
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

// JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key configuration is missing.");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? throw new InvalidOperationException("JWT Issuer configuration is missing.");
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? throw new InvalidOperationException("JWT Audience configuration is missing.");
var jwtExpiryMinutes = builder.Configuration.GetValue<int>("Jwt:ExpiryMinutes", 60);

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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddSignalR();

builder.WebHost.UseStaticWebAssets();

var app = builder.Build();

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

app.MapPost("/api/auth/login", async (LoginRequest login) =>
{
    var cpfLimpo = new string((login.Cpf ?? string.Empty).Where(char.IsDigit).ToArray());
    if (!ValidacoesEntrada.CpfTem11Digitos(cpfLimpo))
        return Results.BadRequest("CPF inválido.");

    if (string.IsNullOrWhiteSpace(login.Senha))
        return Results.BadRequest("Senha é obrigatória.");

    try
    {
        using var connection = new SqlConnection(connectionString);
        var sql = "SELECT Cpf, Nome, Email, SenhaHash FROM Usuarios WHERE Cpf = @Cpf";
        var usuario = await connection.QuerySingleOrDefaultAsync<UsuarioLoginDb>(sql, new { Cpf = cpfLimpo });
        if (usuario == null || !BCrypt.Net.BCrypt.Verify(login.Senha, usuario.SenhaHash))
            return Results.Unauthorized();

        var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key configuration is missing.");
        var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? throw new InvalidOperationException("JWT Issuer configuration is missing.");
        var jwtAudience = builder.Configuration["Jwt:Audience"] ?? throw new InvalidOperationException("JWT Audience configuration is missing.");
        var jwtExpiryMinutes = builder.Configuration.GetValue<int>("Jwt:ExpiryMinutes", 60);

        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(jwtKey);
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, usuario.Cpf),
                new Claim(ClaimTypes.Name, usuario.Nome),
                new Claim(ClaimTypes.Email, usuario.Email),
            }),
            Expires = DateTime.UtcNow.AddMinutes(jwtExpiryMinutes),
            Issuer = jwtIssuer,
            Audience = jwtAudience,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };
        var token = tokenHandler.CreateToken(tokenDescriptor);
        var tokenString = tokenHandler.WriteToken(token);

        var response = new LoginResponse(tokenString, usuario.Cpf, usuario.Nome, usuario.Email, tokenDescriptor.Expires.Value);
        return Results.Ok(response);
    }
    catch (SqlException)
    {
        return Results.Problem(
            title: "Falha de banco de dados",
            detail: "Não foi possível conectar ao SQL Server.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapPost("/api/usuarios", async (UsuarioCreateDto usuario) =>
{
    var cpfLimpo = new string((usuario.Cpf ?? string.Empty).Where(char.IsDigit).ToArray());
    if (!ValidacoesEntrada.CpfTem11Digitos(cpfLimpo))
        return Results.BadRequest("Erro: CPF inválido. Informe 11 dígitos.");

    if (!ValidacoesEntrada.NomeObrigatorio(usuario.Nome) || !ValidacoesEntrada.EmailObrigatorio(usuario.Email))
        return Results.BadRequest("Erro: Nome e e-mail são obrigatórios.");

    if (string.IsNullOrWhiteSpace(usuario.Senha) || usuario.Senha.Length < 6)
        return Results.BadRequest("Erro: Senha deve ter pelo menos 6 caracteres.");

    try
    {
        using var connection = new SqlConnection(connectionString);
        var checkSql = "SELECT COUNT(1) FROM Usuarios WHERE Cpf = @Cpf";
        var cpfExiste = await connection.ExecuteScalarAsync<int>(checkSql, new { Cpf = cpfLimpo });

        if (cpfExiste > 0)
        {
            return Results.BadRequest("Erro: Este CPF já está cadastrado.");
        }

        var senhaHash = BCrypt.Net.BCrypt.HashPassword(usuario.Senha);
        var insertSql = "INSERT INTO Usuarios (Cpf, Nome, Email, SenhaHash) VALUES (@Cpf, @Nome, @Email, @SenhaHash)";
        var novoUsuario = new { Cpf = cpfLimpo, Nome = usuario.Nome.Trim(), Email = usuario.Email.Trim(), SenhaHash = senhaHash };
        await connection.ExecuteAsync(insertSql, novoUsuario);

        var usuarioDto = new UsuarioDto(cpfLimpo, usuario.Nome.Trim(), usuario.Email.Trim());
        return Results.Created($"/api/usuarios/{usuarioDto.Cpf}", usuarioDto);
    }
    catch (SqlException)
    {
        return Results.Problem(
            title: "Falha de banco de dados",
            detail: "Não foi possível conectar ao SQL Server. Verifique a connection string e se o banco está ativo.",
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
        return Results.BadRequest("Dados inválidos.");

    try
    {
        using var connection = new SqlConnection(connectionString);
        // Check seat status
        var seat = await connection.QuerySingleOrDefaultAsync<AssentoDto>(
            "SELECT Id, EventoId, Status, LockedUntil, LockedByCpf FROM Assentos WHERE Id = @Id AND EventoId = @EventoId",
            new { Id = seatId, EventoId = request.EventoId });
        if (seat == null)
            return Results.NotFound("Assento não encontrado.");

        if (seat.Status != "disponivel" && (seat.LockedUntil == null || seat.LockedUntil < DateTime.UtcNow))
            return Results.BadRequest("Assento não está disponível.");

        // Lock seat for 5 minutes
        var lockedUntil = DateTime.UtcNow.AddMinutes(5);
        var updateSql = @"UPDATE Assentos SET Status = 'reservado', LockedUntil = @LockedUntil, LockedByCpf = @LockedByCpf
                          WHERE Id = @Id AND (Status = 'disponivel' OR (LockedUntil IS NOT NULL AND LockedUntil < @Now))";
        var affected = await connection.ExecuteAsync(updateSql, new
        {
            Id = seatId,
            LockedUntil = lockedUntil,
            LockedByCpf = cpf,
            Now = DateTime.UtcNow
        });
        if (affected == 0)
            return Results.BadRequest("Não foi possível bloquear o assento. Pode já estar ocupado.");

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

app.MapPost("/api/assentos/{seatId}/release", async (int seatId, AssentoLockRequest request, HttpContext httpContext) =>
{
    var cpf = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrEmpty(cpf))
        return Results.Unauthorized();

    try
    {
        using var connection = new SqlConnection(connectionString);
        // Only allow release if locked by the same user
        var updateSql = @"UPDATE Assentos SET Status = 'disponivel', LockedUntil = NULL, LockedByCpf = NULL
                          WHERE Id = @Id AND EventoId = @EventoId AND LockedByCpf = @LockedByCpf";
        var affected = await connection.ExecuteAsync(updateSql, new
        {
            Id = seatId,
            EventoId = request.EventoId,
            LockedByCpf = cpf
        });
        if (affected == 0)
            return Results.BadRequest("Assento não está bloqueado por você ou já foi liberado.");

        return Results.Ok(new AssentoLockResponse(true, "Assento liberado.", null));
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
        // Check if already in queue for this event
        var existing = await connection.QuerySingleOrDefaultAsync<FilaDto>(
            "SELECT Id, EventoId, UsuarioCpf, Posicao, Status FROM FilaEvento WHERE EventoId = @EventoId AND UsuarioCpf = @UsuarioCpf AND Status = 'espera'",
            new { EventoId = request.EventoId, UsuarioCpf = cpf });
        if (existing != null)
            return Results.BadRequest("Você já está na fila para este evento.");

        // Get current max position
        var maxPos = await connection.ExecuteScalarAsync<int?>(
            "SELECT MAX(Posicao) FROM FilaEvento WHERE EventoId = @EventoId AND Status = 'espera'",
            new { EventoId = request.EventoId }) ?? 0;
        var newPos = maxPos + 1;

        var insertSql = @"INSERT INTO FilaEvento (EventoId, UsuarioCpf, Posicao, Status, DataEntrada)
                          VALUES (@EventoId, @UsuarioCpf, @Posicao, 'espera', GETDATE())";
        await connection.ExecuteAsync(insertSql, new
        {
            EventoId = request.EventoId,
            UsuarioCpf = cpf,
            Posicao = newPos
        });

        // Notify via SignalR
        return Results.Ok(new FilaResponse(request.EventoId, cpf, newPos, "espera", DateTime.UtcNow, null));
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
        var deleteSql = @"DELETE FROM FilaEvento WHERE EventoId = @EventoId AND UsuarioCpf = @UsuarioCpf AND Status = 'espera'";
        var affected = await connection.ExecuteAsync(deleteSql, new { EventoId = request.EventoId, UsuarioCpf = cpf });
        if (affected == 0)
            return Results.BadRequest("Você não está na fila para este evento.");

        // Update positions of remaining users (optional)
        // For simplicity, we skip reordering

        return Results.Ok(new { Message = "Você saiu da fila." });
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
            return Results.NotFound("Você não está na fila para este evento.");

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

app.MapGet("/api/reservas/{cpf}", async (string cpf) =>
{
    var cpfLimpo = new string((cpf ?? string.Empty).Where(char.IsDigit).ToArray());
    if (!ValidacoesEntrada.CpfTem11Digitos(cpfLimpo))
        return Results.BadRequest("CPF inválido.");

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
                r.Status
            FROM Reservas r
            INNER JOIN Eventos e ON r.EventoId = e.Id
            WHERE r.UsuarioCpf = @Cpf
            ORDER BY r.Id";
        var reservas = await connection.QueryAsync<ReservaDto>(sql, new { Cpf = cpfLimpo });
        return Results.Ok(reservas);
    }
    catch (SqlException)
    {
        return Results.Problem(
            title: "Falha de banco de dados",
            detail: "Não foi possível conectar ao SQL Server.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapPost("/api/reservas", async (ReservaCreateDto reserva) =>
{
    // Clean CPF
    var cpfLimpo = new string((reserva.UsuarioCpf ?? string.Empty).Where(char.IsDigit).ToArray());
    if (!ValidacoesEntrada.CpfTem11Digitos(cpfLimpo))
        return Results.BadRequest("CPF inválido.");

    try
    {
        using var connection = new SqlConnection(connectionString);
        // R1: Validate UsuarioCpf and EventoId exist
        var usuarioExiste = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Usuarios WHERE Cpf = @Cpf",
            new { Cpf = cpfLimpo });
        if (usuarioExiste == 0)
            return Results.BadRequest("Usuário não encontrado.");

        var eventoExiste = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Eventos WHERE Id = @EventoId",
            new { EventoId = reserva.EventoId });
        if (eventoExiste == 0)
            return Results.BadRequest("Evento não encontrado.");

        // R2: Limit of 2 reservations per CPF per EventoId
        var reservasExistentes = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Reservas WHERE UsuarioCpf = @Cpf AND EventoId = @EventoId",
            new { Cpf = cpfLimpo, reserva.EventoId });
        if (reservasExistentes >= 2)
            return Results.BadRequest("Limite de 2 reservas por CPF para este evento já atingido.");

        // R3: Capacity control
        var capacidade = await connection.ExecuteScalarAsync<int>(
            "SELECT CapacidadeTotal FROM Eventos WHERE Id = @EventoId",
            new { reserva.EventoId });
        var reservasTotais = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Reservas WHERE EventoId = @EventoId",
            new { reserva.EventoId });
        if (reservasTotais >= capacidade)
            return Results.BadRequest("Capacidade do evento esgotada.");

        // R4: Coupon validation and price calculation
        decimal precoPadrao = await connection.ExecuteScalarAsync<decimal>(
            "SELECT PrecoPadrao FROM Eventos WHERE Id = @EventoId",
            new { reserva.EventoId });
        decimal valorFinal = precoPadrao;
        if (!string.IsNullOrWhiteSpace(reserva.CupomUtilizado))
        {
            var cupom = await connection.QuerySingleOrDefaultAsync<CupomCreateDto?>(
                "SELECT Codigo, PorcentagemDesconto, ValorMinimoRegra FROM Cupons WHERE Codigo = @Codigo",
                new { Codigo = reserva.CupomUtilizado.Trim().ToUpperInvariant() });
            if (cupom == null)
                return Results.BadRequest("Cupom inválido.");
            if (precoPadrao < cupom.ValorMinimoRegra)
                return Results.BadRequest("O preço do evento é inferior ao valor mínimo requerido pelo cupom.");
            valorFinal = precoPadrao * (1 - cupom.PorcentagemDesconto / 100);
        }

        // Insert reservation
        var insertSql = @"
            INSERT INTO Reservas (UsuarioCpf, EventoId, CupomUtilizado, ValorFinalPago)
            VALUES (@UsuarioCpf, @EventoId, @CupomUtilizado, @ValorFinalPago);
            SELECT CAST(SCOPE_IDENTITY() AS INT)";
        var id = await connection.ExecuteScalarAsync<int>(insertSql, new
        {
            UsuarioCpf = cpfLimpo,
            reserva.EventoId,
            CupomUtilizado = reserva.CupomUtilizado?.Trim().ToUpperInvariant(),
            ValorFinalPago = valorFinal
        });

        var reservaCriada = new ReservaDto(id, cpfLimpo, reserva.EventoId, "", reserva.CupomUtilizado, valorFinal, "pendente");
        return Results.Created($"/api/reservas/{id}", reservaCriada);
    }
    catch (SqlException)
    {
        return Results.Problem(
            title: "Falha de banco de dados",
            detail: "Não foi possível conectar ao SQL Server.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});


app.MapPost("/api/reservas/{reservaId}/pagamentos", async (int reservaId, PagamentoCreateDto pagamento, HttpContext httpContext) =>
{
    var cpf = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrEmpty(cpf))
        return Results.Unauthorized();

    try
    {
        using var connection = new SqlConnection(connectionString);
        // Check reservation exists and belongs to user
        var reserva = await connection.QuerySingleOrDefaultAsync<ReservaDto>(
            "SELECT Id, UsuarioCpf, EventoId, ValorFinalPago FROM Reservas WHERE Id = @Id AND UsuarioCpf = @UsuarioCpf",
            new { Id = reservaId, UsuarioCpf = cpf });
        if (reserva == null)
            return Results.NotFound("Reserva não encontrada ou não pertence ao usuário.");

        // Validate payment method
        if (pagamento.Metodo != "pix" && pagamento.Metodo != "cartao_credito" && pagamento.Metodo != "cartao_debito")
            return Results.BadRequest("Método de pagamento inválido.");

        // Simulate payment processing (random success)
        var random = new Random();
        bool sucesso = random.NextDouble() > 0.3; // 70% success rate
        string status = sucesso ? "aprovado" : "recusado";
        string transacaoId = sucesso ? $"TX-{DateTime.UtcNow.Ticks}" : null;

        // Insert payment record
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
        });

        if (sucesso)
        {
            // Update reservation status to confirmed
            await connection.ExecuteAsync(
                "UPDATE Reservas SET Status = 'confirmada', PagamentoId = @PagamentoId WHERE Id = @Id",
                new { Id = reservaId, PagamentoId = pagamentoId });
            // Optionally update seat status (if seats linked)
        }

        var pagamentoDto = new PagamentoDto(pagamentoId, reservaId, pagamento.Metodo, reserva.ValorFinalPago, status, transacaoId, pagamento.Parcelas, DateTime.UtcNow, DateTime.UtcNow);
        return Results.Ok(pagamentoDto);
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
        // Check reservation exists and belongs to user
        var reserva = await connection.QuerySingleOrDefaultAsync<ReservaDto>(
            "SELECT Id, UsuarioCpf, EventoId, ValorFinalPago, Status FROM Reservas WHERE Id = @Id AND UsuarioCpf = @UsuarioCpf",
            new { Id = reservaId, UsuarioCpf = cpf });
        if (reserva == null)
            return Results.NotFound("Reserva não encontrada ou não pertence ao usuário.");

        // Already cancelled?
        if (reserva.Status == "cancelada")
            return Results.BadRequest("Reserva já está cancelada.");

        // Update reservation status
        await connection.ExecuteAsync(
            "UPDATE Reservas SET Status = 'cancelada' WHERE Id = @Id",
            new { Id = reservaId });

        // If payment exists, mark as refunded
        var pagamento = await connection.QuerySingleOrDefaultAsync<PagamentoDto>(
            "SELECT Id, Status FROM Pagamentos WHERE ReservaId = @ReservaId",
            new { ReservaId = reservaId });
        if (pagamento != null && pagamento.Status == "aprovado")
        {
            await connection.ExecuteAsync(
                "UPDATE Pagamentos SET Status = 'estornado', DataAtualizacao = GETDATE() WHERE Id = @Id",
                new { Id = pagamento.Id });
        }

        // Generate a 10% discount coupon for the user
        var cupomCodigo = $"CANCEL-{reservaId}-{DateTime.UtcNow:yyyyMMddHHmm}";
        var cupomInsert = @"INSERT INTO Cupons (Codigo, PorcentagemDesconto, ValorMinimoRegra, DataExpiracao)
                            VALUES (@Codigo, 10.00, 0.00, DATEADD(month, 3, GETDATE()))";
        await connection.ExecuteAsync(cupomInsert, new { Codigo = cupomCodigo });

        // Return success message with coupon
        return Results.Ok(new
        {
            Message = "Reserva cancelada com sucesso. Um cupom de 10% de desconto foi gerado para você.",
            Cupom = cupomCodigo,
            Desconto = "10%"
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

app.MapHealthChecks("/health");
app.MapFallbackToFile("index.html");

app.Run();

// ==========================================
// MODELOS DE DADOS
// ==========================================
public record EventoDto(int Id, string Nome, int CapacidadeTotal, DateTime DataEvento, decimal PrecoPadrao);
public record EventoCreateDto(string Nome, int CapacidadeTotal, DateTime DataEvento, decimal PrecoPadrao);
public record CupomCreateDto(string Codigo, decimal PorcentagemDesconto, decimal ValorMinimoRegra);
public record UsuarioDto(string Cpf, string Nome, string Email);
public record UsuarioCreateDto(string Cpf, string Nome, string Email, string Senha);
public record UsuarioLoginDb(string Cpf, string Nome, string Email, string SenhaHash);
public record LoginRequest(string Cpf, string Senha);
public record LoginResponse(string Token, string Cpf, string Nome, string Email, DateTime ExpiresAt);
public record AssentoDto(int Id, int EventoId, string Fila, string Numero, string Tipo, decimal PrecoAdicional, string Status, DateTime? LockedUntil, string? LockedByCpf);
public record AssentoCreateDto(string Fila, string Numero, string Tipo, decimal PrecoAdicional);
public record AssentoLockRequest(int SeatId, int EventoId);
public record AssentoLockResponse(bool Success, string? Message, DateTime? LockedUntil);
public record FilaRequest(int EventoId);
public record FilaResponse(int EventoId, string UsuarioCpf, int Posicao, string Status, DateTime DataEntrada, int? TempoEstimado);
public record FilaDto(int Id, int EventoId, string UsuarioCpf, int Posicao, string Status, DateTime DataEntrada, int? TempoEstimado);
public record ReservaDto(int Id, string UsuarioCpf, int EventoId, string EventoNome, string? CupomUtilizado, decimal ValorFinalPago, string Status);
public record ReservaCreateDto(string UsuarioCpf, int EventoId, string? CupomUtilizado);
public record PagamentoDto(int Id, int ReservaId, string Metodo, decimal ValorTotal, string Status, string? TransacaoId, int? Parcelas, DateTime? DataPagamento, DateTime DataAtualizacao);
public record PagamentoCreateDto(string Metodo, int? Parcelas);
