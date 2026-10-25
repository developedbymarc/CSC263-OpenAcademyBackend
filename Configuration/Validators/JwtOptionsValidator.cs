using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace OpenAcademy.Configuration;

public sealed class JwtOptionsValidator : IValidateOptions<JwtOptions>
{
    public ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        // Missing key / unknown algorithm are reported by the annotations
        if (string.IsNullOrEmpty(options.SigningKey)) return ValidateOptionsResult.Success;

        int? minBytes = options.Algorithm switch
        {
            SecurityAlgorithms.HmacSha256 => 32,
            SecurityAlgorithms.HmacSha384 => 48,
            SecurityAlgorithms.HmacSha512 => 64,
            _ => null,
        };
        if (minBytes is null) return ValidateOptionsResult.Success;

        // Assumes the key is used as raw UTF-8 bytes. If you base64-decode it, measure the decoded length instead.
        var actual = Encoding.UTF8.GetByteCount(options.SigningKey);
        return actual < minBytes
            ? ValidateOptionsResult.Fail(
                $"Jwt:SigningKey must be at least {minBytes} bytes for {options.Algorithm} (got {actual}).")
            : ValidateOptionsResult.Success;
    }
}
