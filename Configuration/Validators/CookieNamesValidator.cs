using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace OpenAcademy.Configuration;

/// <summary>Hook type so the cross-cookie rule runs at startup. It holds no settings.</summary>
public sealed class CookieNamesCheck;

/// <summary>Every cookie the API sets must have a distinct name. Add new cookies to the list below.</summary>
public sealed class CookieNamesValidator(IConfiguration config) : IValidateOptions<CookieNamesCheck>
{
    public ValidateOptionsResult Validate(string? name, CookieNamesCheck _)
    {
        var cookies = new[]
        {
            Cookie<JwtOptions>(JwtOptions.SectionName),
            Cookie<RefreshTokenOptions>(RefreshTokenOptions.SectionName),
            Cookie<CsrfOptions>(CsrfOptions.SectionName),
        };

        var failures = cookies
            .Where(c => !string.IsNullOrWhiteSpace(c.Name))   // [Required] reports empty ones
            .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => $"Cookie name '{g.Key}' is used by more than one setting: {string.Join(", ", g.Select(c => c.Key))}.")
            .ToList();

        return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
    }


    private (string Key, string Name) Cookie<T>(string section) where T : ICookieNameOptions, new() =>
        ($"{section}:{nameof(ICookieNameOptions.CookieName)}",
            (config.GetSection(section).Get<T>() ?? new T()).CookieName);
}