using System.ComponentModel.DataAnnotations;

namespace OpenAcademy.Configuration;

public sealed class RefreshTokenOptions : ICookieNameOptions
{
    public const string SectionName = "RefreshToken";
    public const int MaxLifetimeInMinutes = 60 * 24 * 90; // 90 days

    [Required, MinLength(32)]
    public string SigningKey { get; init; } = string.Empty;

    [Required, MinLength(1)]
    public string CookieName { get; init; } = "RefreshToken";

    /// <summary>Sessions that expire after inactivity ("Don't remember me").</summary>
    [Range(1, MaxLifetimeInMinutes)]
    public int ShortExpiryInMinutes { get; init; } = 15;

    /// <summary>Persistent sessions ("Remember me").</summary>
    [Range(1, MaxLifetimeInMinutes)]
    public int LongExpiryInMinutes { get; init; } = 60 * 24 * 7; // 7 days

    public TimeSpan ShortExpiry => TimeSpan.FromMinutes(ShortExpiryInMinutes);
    public TimeSpan LongExpiry => TimeSpan.FromMinutes(LongExpiryInMinutes);
}
