using System.ComponentModel.DataAnnotations;
using Microsoft.IdentityModel.Tokens;

namespace OpenAcademy.Configuration;

public sealed class JwtOptions : ICookieNameOptions
{
    public const string SectionName = "Jwt";
    public const int MaxExpiryInMinutes = 60 * 24;
    public const int MaxClockSkewInSeconds = 60;

    // Minimum length depends on Algorithm, so that's checked in JwtOptionsValidator
    [Required]
    public string SigningKey { get; init; } = string.Empty;

    [Required, Url]
    public string Issuer { get; init; } = string.Empty;

    [Required, Url]
    public string Audience { get; init; } = string.Empty;

    [Required]
    [AllowedValues(SecurityAlgorithms.HmacSha256, SecurityAlgorithms.HmacSha384, SecurityAlgorithms.HmacSha512)]
    public string Algorithm { get; init; } = SecurityAlgorithms.HmacSha256;

    [Range(1, MaxExpiryInMinutes)]
    public int ExpiryInMinutes { get; init; } = 5;
    
    [Range(1, MaxClockSkewInSeconds)]
    public int ClockSkewInSeconds { get; init; } = 30;

    [Required, MinLength(1)]
    public string CookieName { get; init; } = "AccessToken";

    // Get-only, so the binder ignores it
    public TimeSpan Expiry => TimeSpan.FromMinutes(ExpiryInMinutes);
    public TimeSpan ClockSkew => TimeSpan.FromSeconds(ClockSkewInSeconds);
}
