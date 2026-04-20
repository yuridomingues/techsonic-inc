namespace TicketPrime.Server;

public static class PriceCalculator
{
    public static decimal CalculateFinalPrice(decimal basePrice, decimal discountPercentage, decimal couponMinValue, bool couponValid)
    {
        if (!couponValid)
            return ApplyFixedFee(basePrice);

        // Coupon is valid only if discount percentage > 0 and coupon minimum value satisfied
        if (discountPercentage <= 0 || basePrice < couponMinValue)
            return ApplyFixedFee(basePrice);

        var discounted = basePrice * (1 - discountPercentage / 100);
        return ApplyFixedFee(discounted);
    }

    private static decimal ApplyFixedFee(decimal price)
    {
        // Fixed fee of 5%
        return price * 1.05m;
    }

    public static decimal CalculateTotalWithTickets(decimal basePricePerTicket, int quantity, decimal discountPercentage, decimal couponMinValue, bool couponValid)
    {
        var subtotal = basePricePerTicket * quantity;
        return CalculateFinalPrice(subtotal, discountPercentage, couponMinValue, couponValid);
    }
}