using Microsoft.Extensions.Options;
using OpenAcademy.Configuration.Options;
using OpenAcademy.Entities.Users;

namespace OpenAcademy.Services.ApplicationServices.TokenService;

public abstract class AbstractAccessTokenService : IGenericCookieToken
{
    protected readonly JwtOptions _jwtOptions;

    public AbstractAccessTokenService(IOptions<JwtOptions> jwtOptions)
    {
        _jwtOptions = jwtOptions.Value;
    }

    abstract public string Generate(
        ApplicationUser user,
        IEnumerable<string> roles,
        DateTimeOffset expiresAt);

    public string? GetCookie(IRequestCookieCollection cookies)
    {
        cookies.TryGetValue(_jwtOptions.CookieName, out var value);
        return value;
    }

    public void SetCookie(IResponseCookies cookies, string token, TimeSpan maxAge, DateTimeOffset expiresAt)
    {
        var cookieOptions = new CookieOptions
        {
            Path = "/",
            Secure = true,
            HttpOnly = true,
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            MaxAge = maxAge,
            Expires = expiresAt
        };

        cookies.Append(_jwtOptions.CookieName, token, cookieOptions);
    }

    public void UnsetCookie(IResponseCookies cookies)
    {
        cookies.Delete(_jwtOptions.CookieName);
    }
}
