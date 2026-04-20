namespace TicketPrime.Web.Services;

public sealed record ApiResult(bool Success, string? ErrorMessage = null)
{
    public static ApiResult Ok() => new(true);
    public static ApiResult Fail(string message) => new(false, message);
}

public sealed record ApiResult<T>(bool Success, T? Data, string? ErrorMessage = null)
{
    public static ApiResult<T> Ok(T data) => new(true, data);
    public static ApiResult<T> Fail(string message) => new(false, default, message);
}
