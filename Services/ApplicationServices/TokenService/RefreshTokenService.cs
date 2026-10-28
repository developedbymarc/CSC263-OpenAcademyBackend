using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using OpenAcademy.Configuration;

namespace OpenAcademy.Services.ApplicationServices.TokenService;

public sealed class RefreshTokenService : IRefreshTokenService
{
    private RefreshTokenOptions _options;

    public RefreshTokenService(IOptions<RefreshTokenOptions> options)
    {
        _options = options.Value;
    }
    
    public string GeneratePlain()
    {
        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(tokenBytes);
    }

    public string Hash(string plainToken)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(
            _options.SigningKey));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(plainToken));
        return Convert.ToBase64String(hash);
    }
}