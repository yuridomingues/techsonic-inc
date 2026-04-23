using Xunit;
using TicketPrime.Server;

namespace techsonic_inc.Tests;

public class PriceCalculatorTests
{
    [Theory]
    [InlineData(100, 0, 0, 5, false, 105)]
    [InlineData(100, 10, 50, 5, true, 95)]
    [InlineData(100, 10, 150, 5, true, 105)]
    [InlineData(200, 20, 100, 5, true, 165)]
    [InlineData(200, 0, 0, 5, true, 205)]
    public void CalculateFinalPrice_ShouldApplyDiscountAndFee(
        decimal basePrice,
        decimal discountPercentage,
        decimal couponMinValue,
        decimal fixedFeeAmount,
        bool couponValid,
        decimal expected)
    {
        var result = PriceCalculator.CalculateFinalPrice(basePrice, discountPercentage, couponMinValue, fixedFeeAmount, couponValid);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void CalculateTotalWithTickets_ShouldMultiplyQuantity()
    {
        var result = PriceCalculator.CalculateTotalWithTickets(50, 3, 10, 100, 5, true);
        Assert.Equal(140m, result);
    }
}