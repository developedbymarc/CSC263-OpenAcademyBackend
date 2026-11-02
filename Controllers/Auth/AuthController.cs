using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using OpenAcademy.Entities.Users;
using OpenAcademy.Responses;
using OpenAcademy.Services.ApplicationServices.TokenService;
using OpenAcademy.Services.ExternalServices.EmailService;

namespace OpenAcademy.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly IAccessTokenService _accessTokenService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IAntiforgery _antiforgery;
    private readonly IEmailService _emailService;

    private const string ConfirmEmailPath = "/confirm-email";

    public AuthController(
        IAccessTokenService accessTokenService,
        IRefreshTokenService refreshTokenService,
        IAntiforgery antiforgery,
        IEmailService emailService)
    {
        _accessTokenService = accessTokenService;
        _refreshTokenService = refreshTokenService;
        _antiforgery = antiforgery;
        _emailService = emailService;
    }

    [HttpGet("csrf")]
    public ActionResult<ApiResponse<CsrfTokenDto>> GetCsrfToken()
    {
        var tokens = _antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(ApiResponse<CsrfTokenDto>.Succeeded(new CsrfTokenDto(tokens.RequestToken)));
    }

    [HttpGet("refresh")]
    public ActionResult RefreshAccessToken()
    {

    }

}