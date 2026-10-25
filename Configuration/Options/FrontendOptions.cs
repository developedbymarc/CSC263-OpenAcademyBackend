using System.ComponentModel.DataAnnotations;

namespace OpenAcademy.Configuration;

public sealed class FrontendOptions
{
    public const string SectionName = "Frontend";

    /// <summary>Bare origin, e.g. https://app.example.com (no path, no trailing slash). Used for CORS.</summary>
    [Required, Url]
    public string Origin { get; init; } = string.Empty;
}
