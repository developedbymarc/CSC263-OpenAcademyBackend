namespace OpenAcademy.Responses;

public sealed record ApiResponse<T>(bool Success, T? Data, ApiError? Error)
{
    public static ApiResponse<T> Succeeded(T? data) => new(true, data, null);

    public static ApiResponse<T> Failed(
        string code,
        string message,
        IReadOnlyDictionary<string, string[]>? details = null) =>
        new(false, default, new ApiError(code, message, details));
}

public sealed record ApiError(
    string Code,
    string Message,
    IReadOnlyDictionary<string, string[]>? Details = null);
