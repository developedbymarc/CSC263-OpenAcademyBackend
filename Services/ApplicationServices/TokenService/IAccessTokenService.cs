using OpenAcademy.Entities.Users;

namespace OpenAcademy.Services.ApplicationServices.TokenService;

public interface IAccessTokenService
{
    string Generate(
        ApplicationUser user,
        IEnumerable<string> roles,
        DateTimeOffset expiresAt);
}