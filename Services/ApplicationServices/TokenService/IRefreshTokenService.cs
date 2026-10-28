namespace OpenAcademy.Services.ApplicationServices.TokenService;

public interface IRefreshTokenService
{
    string GeneratePlain();
    string Hash(string plainToken);
}