using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace OpenAcademy.Configuration;

public sealed class RefreshTokenOptionsValidator(IConfiguration configuration)
    : IValidateOptions<RefreshTokenOptions>
{
    public ValidateOptionsResult Validate(string? name, RefreshTokenOptions options)
    {
        var failures = new List<string>();

        if (options.ShortExpiryInMinutes > options.LongExpiryInMinutes)
            failures.Add("RefreshToken:ShortExpiryInMinutes must be less than or equal to RefreshToken:LongExpiryInMinutes.");

        // Read from the merged configuration so we don't depend on JwtOptions being valid
        var accessKey = configuration[$"{JwtOptions.SectionName}:{nameof(JwtOptions.SigningKey)}"];
        if (!string.IsNullOrEmpty(accessKey) &&
            string.Equals(accessKey, options.SigningKey, StringComparison.Ordinal))
            failures.Add("RefreshToken:SigningKey must differ from Jwt:SigningKey.");

        return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
    }
}
