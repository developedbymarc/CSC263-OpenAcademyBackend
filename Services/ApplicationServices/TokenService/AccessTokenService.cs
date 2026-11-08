using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpenAcademy.Configuration.Options;
using OpenAcademy.Entities.Users;
using JwtRegisteredClaimNames = Microsoft.IdentityModel.JsonWebTokens.JwtRegisteredClaimNames;

namespace OpenAcademy.Services.ApplicationServices.TokenService;

public sealed class AccessTokenService : AbstractAccessTokenService
{
    public AccessTokenService(IOptions<JwtOptions> jwtOptions) : base(jwtOptions)
    {
    }

    public override string Generate(ApplicationUser user, IEnumerable<string> roles, DateTimeOffset expiresAt)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_jwtOptions.SigningKey));

        var credentials = new SigningCredentials(key, _jwtOptions.Algorithm);

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
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            notBefore: DateTimeOffset.UtcNow.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);


        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
