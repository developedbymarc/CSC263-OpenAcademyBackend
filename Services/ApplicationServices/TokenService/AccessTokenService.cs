using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpenAcademy.Configuration;
using OpenAcademy.Entities.Users;
using JwtRegisteredClaimNames = Microsoft.IdentityModel.JsonWebTokens.JwtRegisteredClaimNames;

namespace OpenAcademy.Services.ApplicationServices.TokenService;

public sealed class AccessTokenService : IAccessTokenService
{
    private readonly JwtOptions _options;

    public AccessTokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
    }

    public string Generate(ApplicationUser user, IEnumerable<string> roles, DateTimeOffset expiresAt)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_options.SigningKey));

        var credentials = new SigningCredentials(key, _options.Algorithm);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email!),
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: DateTimeOffset.UtcNow.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}