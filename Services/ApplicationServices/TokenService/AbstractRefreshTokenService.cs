using Microsoft.Extensions.Options;
using OpenAcademy.Configuration.Options;
using OpenAcademy.Entities.Tokens;

namespace OpenAcademy.Services.ApplicationServices.TokenService;

public abstract class AbstractRefreshTokenService : IGenericCookieToken
{
    protected readonly RefreshTokenOptions _refreshTokenOptions;

    public AbstractRefreshTokenService(IOptions<RefreshTokenOptions> refreshTokenOptions) {
        _refreshTokenOptions = refreshTokenOptions.Value;
    }

    abstract public string GeneratePlain();
    abstract public string Hash(string plainToken);
    abstract public Task<int> RevokeFamilyAsync(Guid userId, Guid familyId, CancellationToken cancellationToken = default);
    abstract public Task<int> RevokeAllForUserAsync(Guid userId, CancellationToken cancellationToken = default);
    abstract public Task RotateTokenAsync(RefreshToken oldToken, RefreshToken newToken);

    public string? GetCookie(IRequestCookieCollection cookies)
    {
        cookies.TryGetValue(_refreshTokenOptions.CookieName, out var value);
        return value;
    }

    public void SetCookie(IResponseCookies cookies, string token, TimeSpan maxAge, DateTimeOffset expiresAt)
    {
        var refreshTokenCookieOptions = new CookieOptions
        {
            Path = "/",
            Secure = true,
            HttpOnly = true,
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            MaxAge = maxAge,
            Expires = expiresAt
        };

        cookies.Append(_refreshTokenOptions.CookieName, token, refreshTokenCookieOptions);
    }

    public void UnsetCookie(IResponseCookies cookies)
    {
        cookies.Delete(_refreshTokenOptions.CookieName);
    }
}
