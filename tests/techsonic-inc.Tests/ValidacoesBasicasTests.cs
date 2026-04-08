using Xunit;

namespace techsonic_inc.Tests;

public class ValidacoesBasicasTests
{
    [Theory]
    [InlineData("12345678901", true)]
    [InlineData("123.456.789-01", true)]
    [InlineData("123", false)]
    [InlineData("", false)]
    public void Deve_validar_cpf_com_11_digitos(string cpf, bool esperado)
    {
        var resultado = ValidacoesEntrada.CpfTem11Digitos(cpf);
        Assert.Equal(esperado, resultado);
    }

    [Fact]
    public void Deve_validar_nome_obrigatorio()
    {
        Assert.True(ValidacoesEntrada.NomeObrigatorio("Evento Tech"));
        Assert.False(ValidacoesEntrada.NomeObrigatorio("   "));
    }

    [Fact]
    public void Deve_validar_email_obrigatorio()
    {
        Assert.True(ValidacoesEntrada.EmailObrigatorio("teste@exemplo.com"));
        Assert.False(ValidacoesEntrada.EmailObrigatorio(null));
    }

    [Fact]
    public void Deve_validar_data_futura_do_evento()
    {
        Assert.True(ValidacoesEntrada.DataEventoFutura(DateTime.Now.AddDays(1)));
        Assert.False(ValidacoesEntrada.DataEventoFutura(DateTime.Now.AddMinutes(-1)));
    }

    [Theory]
    [InlineData(10)]
    [InlineData(1)]
    public void Deve_validar_capacidade_positiva(int capacidade)
    {
        var resultado = ValidacoesEntrada.CapacidadePositiva(capacidade);
        Assert.True(resultado);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Deve_invalidar_capacidade_nao_positiva(int capacidade)
    {
        var resultado = ValidacoesEntrada.CapacidadePositiva(capacidade);
        Assert.False(resultado);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(20)]
    [InlineData(100)]
    public void Deve_validar_percentual_cupom(decimal percentual)
    {
        var resultado = ValidacoesEntrada.PercentualValido(percentual);
        Assert.True(resultado);
    }
}