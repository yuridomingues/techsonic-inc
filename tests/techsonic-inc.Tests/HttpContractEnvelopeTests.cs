using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Microsoft.Data.SqlClient;
using techsonic_inc.Tests.Infrastructure;
using Xunit;

namespace techsonic_inc.Tests;

public sealed class HttpContractEnvelopeTests(TicketPrimeApiFactory factory) : IClassFixture<TicketPrimeApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [SkippableFact]
    public async Task LoginInvalido_DeveRetornar200ComEnvelopeDeFalha()
    {
        factory.EnsureDatabaseAvailable();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new { cpf = "00000000000", senha = "senha-errada" });
        var payload = await response.Content.ReadFromJsonAsync<ApiEnvelope>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.False(payload!.Success);
        Assert.Equal("CPF ou senha invalidos.", payload.Message);
    }

    [SkippableFact]
    public async Task CadastroDuplicado_DeveRetornar400ComEnvelopeDeFalha()
    {
        factory.EnsureDatabaseAvailable();
        using var client = factory.CreateClient();
        const string cpf = "52998224725";

        await factory.SeedVerifiedUserAsync(cpf, "Usuario Duplicado", "duplicado@ticketprime.test", "Senha@1234");

        var response = await client.PostAsJsonAsync("/api/usuarios", new
        {
            cpf,
            nome = "Usuario Duplicado",
            email = "novo@ticketprime.test",
            senha = "Senha@1234"
        });
        var payload = await response.Content.ReadFromJsonAsync<ApiEnvelope>(JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(payload);
        Assert.False(payload!.Success);
        Assert.Equal("Erro: Este CPF já está cadastrado.", payload.Message);
    }

    [SkippableFact]
    public async Task EventoInvalido_DeveRetornar400ComEnvelopeDeFalha()
    {
        factory.EnsureDatabaseAvailable();
        using var client = factory.CreateClient();
        var token = await factory.LoginAsync(client, "00000000000", "admin123");
        TicketPrimeApiFactory.Authorize(client, token);

        var response = await client.PostAsJsonAsync("/api/eventos", new
        {
            nome = string.Empty,
            capacidadeTotal = 100,
            dataEvento = DateTime.UtcNow.AddDays(5),
            precoPadrao = 100,
            tipoEvento = "show",
            descricao = "Evento de teste",
            localNome = "Arena",
            localCidade = "Sao Paulo",
            bannerUrl = (string?)null,
            galeriaTexto = (string?)null,
            taxaFixa = 5,
            status = "ativo"
        });
        var payload = await response.Content.ReadFromJsonAsync<ApiEnvelope>(JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(payload);
        Assert.False(payload!.Success);
        Assert.Equal("Nome do evento é obrigatório.", payload.Message);
    }

    [SkippableFact]
    public async Task ReservaComCupomInvalido_DeveRetornar200ComEnvelopeDeFalha()
    {
        factory.EnsureDatabaseAvailable();
        using var client = factory.CreateClient();
        const string cpf = "11144477735";
        await factory.SeedVerifiedUserAsync(cpf, "Usuario Reserva", "reserva@ticketprime.test", "Senha@1234");

        var token = await factory.LoginAsync(client, cpf, "Senha@1234");
        TicketPrimeApiFactory.Authorize(client, token);

        const int eventoId = 1;
        var response = await client.PostAsJsonAsync("/api/reservas", new
        {
            usuarioCpf = cpf,
            eventoId,
            cupomUtilizado = "INEXISTENTE",
            assentoIds = Array.Empty<int>()
        });
        var payload = await response.Content.ReadFromJsonAsync<ApiEnvelope>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.False(payload!.Success);
        Assert.Equal("Cupom inválido.", payload.Message);
    }

    [SkippableFact]
    public async Task EntrarNaFilaDuasVezes_DeveRetornar200ComEnvelopeDeFalha()
    {
        factory.EnsureDatabaseAvailable();
        using var client = factory.CreateClient();
        const string cpf = "47685099840";
        await factory.SeedVerifiedUserAsync(cpf, "Usuario Fila", "fila@ticketprime.test", "Senha@1234");

        var token = await factory.LoginAsync(client, cpf, "Senha@1234");
        TicketPrimeApiFactory.Authorize(client, token);

        var firstResponse = await client.PostAsJsonAsync("/api/fila/join", new { eventoId = 1 });
        var firstPayload = await firstResponse.Content.ReadFromJsonAsync<ApiEnvelope>(JsonOptions);

        var secondResponse = await client.PostAsJsonAsync("/api/fila/join", new { eventoId = 1 });
        var secondPayload = await secondResponse.Content.ReadFromJsonAsync<ApiEnvelope>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.NotNull(firstPayload);
        Assert.True(firstPayload!.Success);

        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.NotNull(secondPayload);
        Assert.False(secondPayload!.Success);
        Assert.Equal("Você já está na fila para este evento.", secondPayload.Message);
    }

    [SkippableFact]
    public async Task ConsultaReservasPorCpfInvalido_DeveRetornar200ComEnvelopeDeFalha()
    {
        factory.EnsureDatabaseAvailable();
        using var client = factory.CreateClient();
        var token = await factory.LoginAsync(client, "00000000000", "admin123");
        TicketPrimeApiFactory.Authorize(client, token);

        var response = await client.GetAsync("/api/reservas/123");
        var payload = await response.Content.ReadFromJsonAsync<ApiEnvelope>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.False(payload!.Success);
        Assert.Equal("CPF inválido.", payload.Message);
    }

    [SkippableFact]
    public async Task PagamentoComMetodoInvalido_DeveRetornar200ComEnvelopeDeFalha()
    {
        factory.EnsureDatabaseAvailable();
        using var client = factory.CreateClient();
        const string cpf = "39053344705";
        await factory.SeedVerifiedUserAsync(cpf, "Usuario Pagamento", "pagamento@ticketprime.test", "Senha@1234");

        await using (var connection = new SqlConnection(factory.DatabaseConnectionString))
        {
            await connection.OpenAsync();
            var reservaId = await connection.ExecuteScalarAsync<int>(
                @"INSERT INTO Reservas (UsuarioCpf, EventoId, CupomUtilizado, ValorFinalPago)
                  VALUES (@Cpf, 1, NULL, 355.00);
                  SELECT CAST(SCOPE_IDENTITY() AS INT)",
                new { Cpf = cpf });

            var token = await factory.LoginAsync(client, cpf, "Senha@1234");
            TicketPrimeApiFactory.Authorize(client, token);

            var response = await client.PostAsJsonAsync($"/api/reservas/{reservaId}/pagamentos", new
            {
                metodo = "boleto",
                parcelas = (int?)null,
            });
            var payload = await response.Content.ReadFromJsonAsync<ApiEnvelope>(JsonOptions);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotNull(payload);
            Assert.False(payload!.Success);
            Assert.Equal("Método de pagamento inválido.", payload.Message);
        }
    }

    private sealed record ApiEnvelope(bool Success, string Message);
}