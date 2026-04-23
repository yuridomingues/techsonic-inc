using System.Data;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BCrypt.Net;
using Dapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace techsonic_inc.Tests.Infrastructure;

public sealed class TicketPrimeApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string SqlServerConnectionString = "Server=127.0.0.1;User Id=sa;Password=TechSonicInc@2026;TrustServerCertificate=True;";
    private const string DefaultConnectionEnvironmentVariable = "ConnectionStrings__DefaultConnection";
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly string _databaseName = $"TicketPrime_Test_{Guid.NewGuid():N}";
    private string _databaseConnectionString = string.Empty;
    private string? _previousDefaultConnectionString;

    public string DatabaseConnectionString => _databaseConnectionString;
    public bool DatabaseAvailable { get; private set; }
    public string? DatabaseUnavailableReason { get; private set; }

    public async Task InitializeAsync()
    {
        _databaseConnectionString = $"Server=127.0.0.1;Database={_databaseName};User Id=sa;Password=TechSonicInc@2026;TrustServerCertificate=True;";
        _previousDefaultConnectionString = Environment.GetEnvironmentVariable(DefaultConnectionEnvironmentVariable, EnvironmentVariableTarget.Process);
        Environment.SetEnvironmentVariable(DefaultConnectionEnvironmentVariable, _databaseConnectionString, EnvironmentVariableTarget.Process);

        try
        {
            await using var adminConnection = new SqlConnection(SqlServerConnectionString);
            await adminConnection.OpenAsync();
            await adminConnection.ExecuteAsync($"IF DB_ID('{_databaseName}') IS NULL CREATE DATABASE [{_databaseName}]");

            var schema = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "db", "schemaTicket.sql"));
            await using var databaseConnection = new SqlConnection(_databaseConnectionString);
            await databaseConnection.OpenAsync();
            await databaseConnection.ExecuteAsync(schema);

            DatabaseAvailable = true;
            DatabaseUnavailableReason = null;
        }
        catch (Exception ex)
        {
            DatabaseAvailable = false;
            DatabaseUnavailableReason = $"SQL Server de integração indisponível: {ex.Message}";
        }
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();

        Environment.SetEnvironmentVariable(DefaultConnectionEnvironmentVariable, _previousDefaultConnectionString, EnvironmentVariableTarget.Process);

        if (!DatabaseAvailable || string.IsNullOrWhiteSpace(_databaseName))
            return;

        try
        {
            await using var adminConnection = new SqlConnection(SqlServerConnectionString);
            await adminConnection.OpenAsync();
            await adminConnection.ExecuteAsync(
                $@"IF DB_ID('{_databaseName}') IS NOT NULL
                   BEGIN
                       ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                       DROP DATABASE [{_databaseName}];
                   END");
        }
        catch
        {
            // Cleanup must not fail the suite when the local SQL environment goes down mid-run.
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("IntegrationTesting");
        builder.UseSetting("ConnectionStrings:DefaultConnection", _databaseConnectionString);
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _databaseConnectionString,
                ["Jwt:Key"] = "TechSonicInc2026SuperSecretKeyForJWTTokenGeneration",
                ["Jwt:Issuer"] = "TicketPrime",
                ["Jwt:Audience"] = "TicketPrimeUsers",
                ["Jwt:ExpiryMinutes"] = "60",
                ["Email:FromAddress"] = "no-reply@ticketprime.local",
                ["Email:FromName"] = "TicketPrime Tests",
                ["Email:VerificationCodeExpiryMinutes"] = "10",
                ["Email:PickupDirectory"] = Path.Combine(Path.GetTempPath(), "ticketprime-test-emails", _databaseName),
                ["Email:Smtp:Host"] = string.Empty,
                ["Email:Smtp:Port"] = "587",
                ["Email:Smtp:EnableSsl"] = "true",
                ["Email:Smtp:User"] = string.Empty,
                ["Email:Smtp:Password"] = string.Empty,
            });
        });
    }

    public async Task SeedVerifiedUserAsync(string cpf, string nome, string email, string senha, bool isAdmin = false)
    {
        EnsureDatabaseAvailable();

        const string sql = @"
            INSERT INTO Usuarios (Cpf, Nome, Email, SenhaHash, EmailVerificado, EmailVerificadoEm, IsAdmin)
            VALUES (@Cpf, @Nome, @Email, @SenhaHash, 1, GETUTCDATE(), @IsAdmin)";

        await using var connection = new SqlConnection(_databaseConnectionString);
        await connection.OpenAsync();
        await connection.ExecuteAsync(sql, new
        {
            Cpf = cpf,
            Nome = nome,
            Email = email,
            SenhaHash = BCrypt.Net.BCrypt.HashPassword(senha),
            IsAdmin = isAdmin,
        });
    }

    public async Task<string> LoginAsync(HttpClient client, string cpf, string senha)
    {
        EnsureDatabaseAvailable();

        var response = await client.PostAsJsonAsync("/api/auth/login", new { cpf, senha });
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<LoginAttemptEnvelope>(JsonOptions);
        if (payload?.Success != true || string.IsNullOrWhiteSpace(payload.Session?.Token))
            throw new InvalidOperationException("Nao foi possivel obter token para o teste.");

        return payload.Session.Token;
    }

    public static void Authorize(HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public void EnsureDatabaseAvailable()
    {
        Skip.IfNot(DatabaseAvailable, DatabaseUnavailableReason ?? "SQL Server de integração indisponível.");
    }

    private sealed record LoginAttemptEnvelope(bool Success, string Message, LoginSession? Session);
    private sealed record LoginSession(string Token);
}