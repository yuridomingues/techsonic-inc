namespace TicketPrime.Server;

public static class PriceCalculator
{
    public static decimal CalculateFinalPrice(decimal basePrice, decimal discountPercentage, decimal couponMinValue, decimal fixedFeeAmount, bool couponValid)
    {
        var finalPrice = basePrice;

        if (!couponValid)
            return ApplyFixedFee(finalPrice, fixedFeeAmount);

        if (discountPercentage <= 0 || basePrice < couponMinValue)
            return ApplyFixedFee(finalPrice, fixedFeeAmount);

        finalPrice = basePrice * (1 - discountPercentage / 100);
        return ApplyFixedFee(finalPrice, fixedFeeAmount);
    }

    private static decimal ApplyFixedFee(decimal price, decimal fixedFeeAmount)
    {
        return decimal.Round(price + Math.Max(fixedFeeAmount, 0), 2, MidpointRounding.AwayFromZero);
    }

    public static decimal CalculateTotalWithTickets(decimal basePricePerTicket, int quantity, decimal discountPercentage, decimal couponMinValue, decimal fixedFeeAmount, bool couponValid)
    {
        var subtotal = basePricePerTicket * quantity;
        return CalculateFinalPrice(subtotal, discountPercentage, couponMinValue, fixedFeeAmount, couponValid);
    }
}