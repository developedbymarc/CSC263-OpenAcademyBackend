using System.ComponentModel.DataAnnotations;

namespace OpenAcademy.Configuration;

public sealed class CsrfOptions : ICookieNameOptions
{
    public const string SectionName = "Csrf";
    public const string HeaderName = "X-CSRF-TOKEN";
    public const bool HttpOnly = true;
    public const CookieSecurePolicy SecurePolicy = CookieSecurePolicy.Always;
    public const SameSiteMode SameSite = SameSiteMode.Strict;
    public const string Path = "/";

    // __Host- cookies must be Secure, Path=/, and have no Domain attribute
    [Required]
    [RegularExpression(@"^__Host-[A-Za-z0-9._-]+\z",
        ErrorMessage = "Csrf:CookieName must start with '__Host-' followed by letters, digits, '.', '_' or '-'.")]
    public string CookieName { get; init; } = "__Host-csrf";
}
