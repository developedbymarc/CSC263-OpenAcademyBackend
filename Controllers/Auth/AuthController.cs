using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OpenAcademy.Configuration.Options;
using OpenAcademy.Entities.Users;
using OpenAcademy.Responses;
using OpenAcademy.Services.ApplicationServices.TokenService;
using OpenAcademy.Services.ExternalServices.EmailService;
using OpenAcademy.Entities.Tokens;
using Microsoft.EntityFrameworkCore;
using OpenAcademy.Data;

namespace OpenAcademy.Controllers;

[Route("[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    // services
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationUser> _roleManager;
    private readonly AbstractAccessTokenService _accessTokenService;
    private readonly AbstractRefreshTokenService _refreshTokenService;
    private readonly IAntiforgery _antiforgery;
    private readonly IEmailService _emailService;

    // options
    private readonly JwtOptions _jwtOptions;
    private readonly RefreshTokenOptions _refreshTokenOptions;
    private readonly FrontendOptions _frontendOptions;

    // logger
    private readonly ILogger<AuthController> _logger;

    // local constants
    private const string ControllerName = "Auth";
    private const string ConfirmEmailPath = "/confirm-email";
    private const string SignInPath = "/sign-in";

    public AuthController(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationUser> roleManager,
        AbstractAccessTokenService accessTokenService,
        AbstractRefreshTokenService refreshTokenService,
        IAntiforgery antiforgery,
        IEmailService emailService,
        IOptions<FrontendOptions> frontendOptions,
        IOptions<JwtOptions> jwtOptions,
        IOptions<RefreshTokenOptions> refreshTokenOptions,
        ILogger<AuthController> logger)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _roleManager = roleManager;
        _accessTokenService = accessTokenService;
        _refreshTokenService = refreshTokenService;
        _antiforgery = antiforgery;
        _emailService = emailService;
        _frontendOptions = frontendOptions.Value;
        _jwtOptions = jwtOptions.Value;
        _refreshTokenOptions = refreshTokenOptions.Value;
        _logger = logger;
    }

    [HttpGet("csrf")]
    public ActionResult<ApiResponse<CsrfTokenDto>> GetCsrfToken()
    {
        var tokens = _antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(ApiResponse<CsrfTokenDto>.Succeeded(new CsrfTokenDto(tokens.RequestToken)));
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<ApiResponse>> RefreshAccessToken()
    {
        if (!Request.Cookies.TryGetValue(_refreshTokenOptions.CookieName, out var oldToken))
        {
            return Unauthorized(ApiResponse.Failed(message: "Refresh token missing"));
        }

        var oldTokenHash = _refreshTokenService.Hash(oldToken);
        var oldTokenRecord = await _dbContext.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.TokenHash == oldTokenHash);

        // ---- guards ----
        if (oldTokenRecord == null)
        {
            return Unauthorized(ApiResponse.Failed(message: "Invalid refresh token"));
        }

        if (oldTokenRecord.IsRevoked || oldTokenRecord.IsExpired)
        {
            await _refreshTokenService.RevokeFamilyAsync(oldTokenRecord);
            return Unauthorized(ApiResponse.Failed(message: "Invalid refresh token"));
        }

        var refreshUser = oldTokenRecord.User;
        if (refreshUser.LockoutEnd.HasValue && refreshUser.LockoutEnd > DateTimeOffset.UtcNow)
        {
            return Unauthorized(ApiResponse.Failed(message: "Account suspended or banned"));
        }

        // ---- issuing new tokes ----
        var newToken = _refreshTokenService.GeneratePlain();
        var newTokenHash = _refreshTokenService.Hash(newToken);
        var newTokenMaxAge = oldTokenRecord.IsPersistent
            ? _refreshTokenOptions.LongExpiry
            : _refreshTokenOptions.ShortExpiry;

        var newTokenRecord = new RefreshToken
        {
            // inherited from previous
            UserId = oldTokenRecord.UserId,
            FamilyId = oldTokenRecord.FamilyId,
            IsPersistent = oldTokenRecord.IsPersistent,

            // new values
            TokenHash = newTokenHash,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.Add(newTokenMaxAge),
            Ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            UserAgent = Request.Headers.UserAgent.ToString()
        };

        await _refreshTokenService.RotateTokenAsync(oldTokenRecord, newTokenRecord);

        var roles = await _userManager.GetRolesAsync(oldTokenRecord.User);
        var accessTokenExpiresAt = DateTimeOffset.UtcNow.Add(_jwtOptions.Expiry);
        var accessToken = _accessTokenService.Generate(
            oldTokenRecord.User,
            roles,
            accessTokenExpiresAt
        );

        _accessTokenService.SetCookie(
            Response.Cookies,
            accessToken,
            _jwtOptions.Expiry,
            accessTokenExpiresAt
        );
        _refreshTokenService.SetCookie(
            Response.Cookies,
            newToken,
            newTokenMaxAge,
            newTokenRecord.ExpiresAt);

        return Ok(ApiResponse.Succeeded(message: "Session refreshed"));
    }

    [HttpPost("signup")]
    public async Task<ActionResult<ApiResponse>> SignUp()
    {
        throw new NotImplementedException();
    }

    [HttpPost("signin")]
    public async Task<ActionResult<ApiResponse>> SignIn()
    {
        throw new NotImplementedException();
    }

    [HttpGet("confirm-email")]
    public async Task<ActionResult<ApiResponse>> ConfirmEmail([FromQuery] EmailConfirmationDto dto)
    {
        var user = await _userManager.FindByIdAsync(dto.UserId);
        if (user == null)
        {
            return BadRequest(ApiResponse.Failed(message: ""));
        }

        var result = await _userManager.ConfirmEmailAsync(user, Uri.UnescapeDataString(dto.Token));
        if (!result.Succeeded)
        {
            return BadRequest(ApiResponse.Failed(message: "Email"));
        }

        var returnUrl = _frontendOptions.Origin + SignInPath + "?emailConfirmed=true";

        return Redirect(returnUrl);
    }


}
