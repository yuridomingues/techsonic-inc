using Xunit;

namespace techsonic_inc.Tests;

public class UnitTest1
{
    [Fact]
    public void Deve_retornar_verdadeiro()
    {
        // Arrange
        var resultado = true;

        // Act
        var valorObtido = resultado;

        // Assert
        Assert.True(valorObtido);
    }
}
