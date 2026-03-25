using Xunit;

namespace techsonic_inc.Tests;

public class ValidacoesBasicasTests
{
    [Theory]
    [InlineData(1, 1, 2)]
    [InlineData(2, 3, 5)]
    [InlineData(10, 5, 15)]
    public void Deve_somar_valores_corretamente(int a, int b, int esperado)
    {
        // Act
        var resultado = a + b;

        // Assert
        Assert.Equal(esperado, resultado);
    }
}