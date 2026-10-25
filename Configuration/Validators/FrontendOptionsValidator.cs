using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace OpenAcademy.Configuration;

public sealed class FrontendOptionsValidator(IHostEnvironment env) : IValidateOptions<FrontendOptions>
{
    public ValidateOptionsResult Validate(string? name, FrontendOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Origin)) return ValidateOptionsResult.Success; // [Required] reports it

        if (!Uri.TryCreate(options.Origin, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return ValidateOptionsResult.Fail("Frontend:Origin must be an absolute http(s) URL.");

        // CORS compares origins as exact strings, so "https://x.com/" would silently never match
        var normalized = uri.GetLeftPart(UriPartial.Authority);
        if (!string.Equals(options.Origin, normalized, StringComparison.OrdinalIgnoreCase))
            return ValidateOptionsResult.Fail(
                $"Frontend:Origin must be a bare origin such as '{normalized}' (no path, query, trailing slash or default port).");

        if (!env.IsDevelopment() && uri.Scheme != Uri.UriSchemeHttps)
            return ValidateOptionsResult.Fail("Frontend:Origin must use https outside Development.");

        return ValidateOptionsResult.Success;
    }
}
