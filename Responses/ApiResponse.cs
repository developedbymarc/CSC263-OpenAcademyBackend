namespace OpenAcademy.Responses;

public sealed record ApiResponse(bool Success, ApiSuccessMessage? Data, ApiError? Error)
{
    public static ApiResponse Succeeded(string message) => new(true, new ApiSuccessMessage(message), null);
    public static ApiResponse Failed(
        string message,
        string code = "",
        IReadOnlyDictionary<string, string[]>? details = null) => new(
            false,
            default,
            new ApiError(code, message, details));
}

public sealed record ApiResponse<T>(bool Success, T? Data, ApiError? Error)
{
    public static ApiResponse<T> Succeeded(T? data) => new(true, data, null);

    public static ApiResponse<T> Failed(
        string message,
        string code = "",
        IReadOnlyDictionary<string, string[]>? details = null) => new(
            false,
            default,
            new ApiError(code, message, details));
}

public sealed record ApiSuccessMessage(string Message);
public sealed record ApiError(
    string Code,
    string Message,
    IReadOnlyDictionary<string, string[]>? Details = null);
