namespace OpenAcademy.Services.ApplicationServices.TokenService;

public interface IGenericCookieToken
{
    public string? GetCookie(IRequestCookieCollection cookies);
    public void SetCookie(IResponseCookies cookies, string token, TimeSpan maxAge, DateTimeOffset expiresAt);
    public void UnsetCookie(IResponseCookies cookies);
}
