using Xunit;
using TicketPrime.Server;

namespace techsonic_inc.Tests;

public class PriceCalculatorTests
{
    [Theory]
    [InlineData(100, 0, 0, false, 105)] // no coupon, fixed fee 5%
    [InlineData(100, 10, 50, true, 94.5)] // 10% discount, then fee
    [InlineData(100, 10, 150, true, 105)] // coupon min value not met, no discount
    [InlineData(200, 20, 100, true, 168)] // 20% discount, fee
    [InlineData(200, 0, 0, true, 210)] // coupon valid but discount zero
    public void CalculateFinalPrice_ShouldApplyDiscountAndFee(
        decimal basePrice,
        decimal discountPercentage,
        decimal couponMinValue,
        bool couponValid,
        decimal expected)
    {
        var result = PriceCalculator.CalculateFinalPrice(basePrice, discountPercentage, couponMinValue, couponValid);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void CalculateTotalWithTickets_ShouldMultiplyQuantity()
    {
        var result = PriceCalculator.CalculateTotalWithTickets(50, 3, 10, 100, true);
        // subtotal = 150, discount 10% => 135, fee 5% => 141.75
        Assert.Equal(141.75m, result);
    }
}