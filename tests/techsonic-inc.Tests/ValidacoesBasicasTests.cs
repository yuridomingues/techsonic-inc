using Xunit;

namespace techsonic_inc.Tests;

public class ValidacoesBasicasTests
{
    [Theory]
    [InlineData("52998224725", true)]
    [InlineData("529.982.247-25", true)]
    [InlineData("11111111111", false)]
    [InlineData("12345678901", false)]
    [InlineData("123", false)]
    [InlineData("", false)]
    public void Deve_validar_cpf_real(string cpf, bool esperado)
    {
        var resultado = ValidacoesEntrada.CpfValido(cpf);
        Assert.Equal(esperado, resultado);
    }

    [Theory]
    [InlineData("Maria Silva", true)]
    [InlineData("Joao", false)]
    [InlineData("A B", false)]
    [InlineData("   ", false)]
    public void Deve_validar_nome_com_nome_e_sobrenome(string nome, bool esperado)
    {
        Assert.Equal(esperado, ValidacoesEntrada.NomeCompletoValido(nome));
    }

    [Fact]
    public void Deve_validar_email_obrigatorio()
    {
        Assert.True(ValidacoesEntrada.EmailObrigatorio("teste@exemplo.com"));
        Assert.False(ValidacoesEntrada.EmailObrigatorio(null));
    }

    [Theory]
    [InlineData("teste@exemplo.com", true)]
    [InlineData("usuario@gmail.com", true)]
    [InlineData("usuario@outlook.com", true)]
    [InlineData("usuario@hotmail.com", true)]
    [InlineData("usuario+tag@ticketprime.com.br", true)]
    [InlineData("usuario@gnail.com", false)]
    [InlineData("email-invalido", false)]
    [InlineData(" ", false)]
    public void Deve_validar_formato_de_email(string email, bool esperado)
    {
        Assert.Equal(esperado, ValidacoesEntrada.EmailValido(email));
    }

    [Fact]
    public void Deve_sugerir_correcao_para_dominio_comum_de_email()
    {
        var erro = ValidacoesEntrada.ObterErroEmail("usuario@gnail.com");

        Assert.NotNull(erro);
        Assert.Contains("gmail.com", erro);
    }

    [Fact]
    public void Deve_validar_data_futura_do_evento()
    {
        Assert.True(ValidacoesEntrada.DataEventoFutura(DateTime.UtcNow.AddDays(1)));
        Assert.False(ValidacoesEntrada.DataEventoFutura(DateTime.UtcNow.AddMinutes(-1)));
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

    [Theory]
    [InlineData("Ab12!xyz", true)]
    [InlineData("ab12!xyz", false)]
    [InlineData("AB12!XYZ", false)]
    [InlineData("Ab11!xyz", false)]
    [InlineData("Ab1111!x", false)]
    [InlineData("Ab12xyza", false)]
    public void Deve_validar_regras_fortes_de_senha(string senha, bool esperado)
    {
        Assert.Equal(esperado, ValidacoesEntrada.SenhaForteValida(senha));
    }

    [Fact]
    public void Deve_listar_erros_de_senha_quando_regras_nao_sao_atendidas()
    {
        var erros = ValidacoesEntrada.ListarErrosSenha("abcdefg1");

        Assert.Contains(erros, erro => erro.Contains("maiuscula", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(erros, erro => erro.Contains("especial", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("show", true)]
    [InlineData("teatro", true)]
    [InlineData("stadium", false)]
    [InlineData("", true)]
    public void Deve_validar_tipo_de_evento(string? tipoEvento, bool esperado)
    {
        Assert.Equal(esperado, ValidacoesEntrada.TipoEventoValido(tipoEvento));
    }

    [Theory]
    [InlineData("123456", true)]
    [InlineData("12345", false)]
    [InlineData("12a456", false)]
    public void Deve_validar_codigo_de_verificacao(string codigo, bool esperado)
    {
        Assert.Equal(esperado, ValidacoesEntrada.CodigoVerificacaoValido(codigo));
    }

    [Fact]
    public void Deve_validar_hash_do_admin_padrao_do_schema()
    {
        const string senhaPadrao = "admin123";
        const string hashAdmin = "$2a$11$VsP1Gl7H66fBSngtBZoYTeM6j2e05rwDo7d35OfXaDHf2INtgcGv6";

        Assert.True(BCrypt.Net.BCrypt.Verify(senhaPadrao, hashAdmin));
    }
}