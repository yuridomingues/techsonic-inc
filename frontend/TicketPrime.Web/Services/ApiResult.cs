namespace TicketPrime.Web.Services;

public sealed record ApiResult(bool Success, string? ErrorMessage = null)
{
    public static ApiResult Ok() => new(true);
    public static ApiResult Fail(string message) => new(false, message);
}
