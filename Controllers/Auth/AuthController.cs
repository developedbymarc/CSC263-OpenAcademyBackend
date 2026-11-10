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
using OpenAcademy.Constants;
using OpenAcademy.Entities.Users.RoleProfiles;
using OpenAcademy.Utils;
using System.Collections.ObjectModel;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.WebUtilities;
using OpenAcademy.Entities;

namespace OpenAcademy.Controllers;

[ApiController]
[Route("[controller]")]
public class AuthController : ControllerBase
{
    // services
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly AbstractAccessTokenService _accessTokenService;
    private readonly AbstractRefreshTokenService _refreshTokenService;
    private readonly IAntiforgery _antiforgery;
    private readonly IEmailService _emailService;

    // options
    private readonly JwtOptions _jwtOptions;
    private readonly RefreshTokenOptions _refreshTokenOptions;
    private readonly FrontendOptions _frontendOptions;

    // utils
    private readonly ILogger<AuthController> _logger;
    private readonly LinkGenerator _linkGenerator;

    // local constants
    private const string ControllerName = "Auth";
    private const string ResetPasswordPath = "/reset-password";
    private const string SignInPath = "/sign-in";

    public AuthController(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        SignInManager<ApplicationUser> signInManager,
        AbstractAccessTokenService accessTokenService,
        AbstractRefreshTokenService refreshTokenService,
        IAntiforgery antiforgery,
        IEmailService emailService,
        IOptions<FrontendOptions> frontendOptions,
        IOptions<JwtOptions> jwtOptions,
        IOptions<RefreshTokenOptions> refreshTokenOptions,
        ILogger<AuthController> logger,
        LinkGenerator linkGenerator)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _roleManager = roleManager;
        _signInManager = signInManager;
        _accessTokenService = accessTokenService;
        _refreshTokenService = refreshTokenService;
        _antiforgery = antiforgery;
        _emailService = emailService;
        _frontendOptions = frontendOptions.Value;
        _jwtOptions = jwtOptions.Value;
        _refreshTokenOptions = refreshTokenOptions.Value;
        _logger = logger;
        _linkGenerator = linkGenerator;
    }

    private Guid? GetCurrentUserId() =>
        Guid.TryParse(_userManager.GetUserId(User), out var id) ? id : null;

    [HttpGet("csrf")]
    public ActionResult<ApiResponse<CsrfTokenDto>> GetCsrfToken()
    {
        var tokens = _antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(ApiResponse<CsrfTokenDto>.Succeeded(new CsrfTokenDto(
            RequestToken: tokens.RequestToken))
        );
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
        if (oldTokenRecord is null)
        {
            return Unauthorized(ApiResponse.Failed(message: "Invalid refresh token"));
        }

        if (oldTokenRecord.IsRevoked || oldTokenRecord.IsExpired)
        {
            await _refreshTokenService.RevokeFamilyAsync(oldTokenRecord.Id, oldTokenRecord.FamilyId);
            return Unauthorized(ApiResponse.Failed(message: "Invalid refresh token"));
        }

        var refreshUser = oldTokenRecord.User;
        if (refreshUser.Status == AccountStatus.Disabled ||
            (refreshUser.LockoutEnd.HasValue && refreshUser.LockoutEnd > DateTimeOffset.UtcNow))
        {
            return Unauthorized(ApiResponse.Failed(message: "Account disabled or locked"));
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
            ReplacedById = oldTokenRecord.Id,

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

    [Authorize]
    [HttpPost("devices-signed-in")]
    public async Task<ActionResult<ApiResponse<IEnumerable<DeviceSessionDto>>>> GetDevicesSignedIn()
    {
        if (!Request.Cookies.TryGetValue(_refreshTokenOptions.CookieName, out var refreshToken))
        {
            return Unauthorized(ApiResponse.Failed(message: "Refresh token missing"));
        }

        var refreshTokenHash = _refreshTokenService.Hash(refreshToken);
        var refreshTokenRecord = await _dbContext.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.TokenHash == refreshTokenHash);

        if (refreshTokenRecord is null)
        {
            return Unauthorized(ApiResponse.Failed(message: "Invalid refresh token"));
        }

        var userId = refreshTokenRecord.UserId;

        var activeSessions = await _dbContext.RefreshTokens
            .Where(rt => rt.UserId == userId && !rt.IsRevoked && !rt.IsExpired)
            .Select(rt => new DeviceSessionDto(
                RefreshTokenId: rt.Id.ToString(),
                RefreshTokenFamilyId: rt.FamilyId.ToString(),
                CreatedAt: rt.CreatedAt,
                ExpiresAt: rt.ExpiresAt,
                IsPersistent: rt.IsPersistent,
                Ip: rt.Ip,
                UserAgent: rt.UserAgent)
            )
            .ToListAsync();

        return Ok(ApiResponse<IEnumerable<DeviceSessionDto>>.Succeeded(activeSessions));
    }

#pragma warning disable
    [Authorize]
    [HttpPost("signout")]
    public async Task<ActionResult<ApiResponse>> SignOut()
    {
        if (GetCurrentUserId() is not { } userId)
        {
            return Unauthorized(ApiResponse.Failed(message: "Invalid session"));
        }

        var refreshToken = _refreshTokenService.GetCookie(Request.Cookies);
        if (refreshToken is not null)
        {
            var refreshTokenHash = _refreshTokenService.Hash(refreshToken);
            var refreshTokenRecord = await _dbContext.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.TokenHash == refreshTokenHash);

            if (refreshTokenRecord is not null)
            {
                await _refreshTokenService.RevokeFamilyAsync(userId, refreshTokenRecord.Id);
            }
        }

        _accessTokenService.UnsetCookie(Response.Cookies);
        _refreshTokenService.UnsetCookie(Response.Cookies);
        return Ok(ApiResponse.Succeeded(message: "Signed out successfully"));
    }
#pragma warning restore

    [Authorize]
    [HttpPost("signout-all-devices")]
    public async Task<ActionResult<ApiResponse>> SignOutAllDevices()
    {
        if (GetCurrentUserId() is not { } userId)
        {
            return Unauthorized(ApiResponse.Failed(message: "Invalid session"));
        }

        // Identify the user from the access token, not the refresh cookie.
        // This works even if the cookie is missing or already invalid.
        await _refreshTokenService.RevokeAllForUserAsync(userId);

        _accessTokenService.UnsetCookie(Response.Cookies);
        _refreshTokenService.UnsetCookie(Response.Cookies);
        return Ok(ApiResponse.Succeeded(message: "Signed out of all devices"));
    }

    [Authorize]
    [HttpPost("signout-device")]
    public async Task<ActionResult<ApiResponse>> SignOutDevice([FromBody] SignOutDeviceDto signOutDeviceDto)
    {
        if (GetCurrentUserId() is not { } userId)
        {
            return Unauthorized(ApiResponse.Failed(message: "Invalid session"));
        }

        var revoked = await _refreshTokenService.RevokeFamilyAsync(userId, signOutDeviceDto.RefreshTokenFamilyId);
        if (revoked == 0)
        {
            // Unknown, already revoked, or belongs to someone else: same answer for all.
            return NotFound(ApiResponse.Failed(message: "Session not found"));
        }

        // If the user just signed out the device they're on, clear its cookies too.
        var currentToken = _refreshTokenService.GetCookie(Request.Cookies);
        if (currentToken is not null)
        {
            var hash = _refreshTokenService.Hash(currentToken);
            var isCurrentDevice = await _dbContext.RefreshTokens
                .AnyAsync(rt => rt.TokenHash == hash
                                && rt.UserId == userId
                                && rt.FamilyId == signOutDeviceDto.RefreshTokenFamilyId);

            if (isCurrentDevice)
            {
                _accessTokenService.UnsetCookie(Response.Cookies);
                _refreshTokenService.UnsetCookie(Response.Cookies);
            }
        }

        return Ok(ApiResponse.Succeeded(message: "Device signed out"));
    }

    [HttpPost("signup")]
    public async Task<ActionResult<ApiResponse>> SignUp([FromBody] SignUpDto signUpDto)
    {
        // check requested role
        if (!RoleNames.CanSignUp.Contains(signUpDto.Role))
        {
            return BadRequest(ApiResponse.Failed(message: $"Cannot sign up as {signUpDto.Role}"));
        }

        // check mismatch
        if (signUpDto.Password != signUpDto.ConfirmedPassword)
        {
            return BadRequest(ApiResponse.Failed(message: "Passwords do not match"));
        }

        // check if email exists already
        if (await _userManager.FindByEmailAsync(signUpDto.Email) is not null)
        {
            return BadRequest(ApiResponse.Failed(message: "Email already in use"));
        }

        // user creation tx
        IdentityResult? failure = null;
        ApplicationUser? createdUser = null;

        try
        {
            var strategy = _dbContext.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                failure = null;
                createdUser = null;
                _dbContext.ChangeTracker.Clear();

                await using var tx = await _dbContext.Database.BeginTransactionAsync();

                var user = new ApplicationUser
                {
                    FirstName = signUpDto.FirstName,
                    LastName = signUpDto.LastName,
                    Email = signUpDto.Email,
                    CreatedAt = DateTimeOffset.UtcNow,
                    Status = AccountStatus.Active,
                };

                var createResult = await _userManager.CreateAsync(user, signUpDto.Password);
                if (!createResult.Succeeded)
                {
                    failure = createResult;
                    await tx.RollbackAsync();
                    return;
                }

                var roleResult = await _userManager.AddToRoleAsync(user, signUpDto.Role);
                if (!roleResult.Succeeded)
                {
                    failure = roleResult;
                    await tx.RollbackAsync();
                    return;
                }

                switch (signUpDto.Role)
                {
                    case RoleNames.Instructor:
                    {
                        await _dbContext.InstructorProfiles.AddAsync(
                            new InstructorProfile { UserId = user.Id });
                        break;
                    }

                    case RoleNames.Student:
                    {
                        await _dbContext.StudentProfiles.AddAsync(
                            new StudentProfile { UserId = user.Id });
                        break;
                    }

                    default:
                    {
                        _logger.LogWarning("Unsupported role: {Role}", signUpDto.Role);
                        throw new ArgumentOutOfRangeException(
                            nameof(signUpDto), signUpDto.Role, "Unsupported role.");
                    }
                }

                await _dbContext.SaveChangesAsync();

                await tx.CommitAsync();
                createdUser = user;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sign up failed unexpectedly");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                ApiResponse.Failed(
                    code: "SIGNUP",
                    message: "Something went wrong while creating your account",
                    details: failure?.ToDetails()
                )
            );
        }

        if (failure is not null)
        {
            return BadRequest(ApiResponse.Failed(
                code: "SIGNUP",
                message: "Something went wrong while creating your account",
                details: failure.ToDetails())
            );
        }

        // Everything below runs AFTER commit. An email failure must not undo
        // or fail the registration (core functionality survives optional services).
        try
        {
            await SendConfirmationEmailAsync(createdUser!);
            return StatusCode(StatusCodes.Status201Created,
                ApiResponse.Succeeded(message: "User registered successfully")
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not send confirmation email for user {UserId}", createdUser!.Id);
            return StatusCode(StatusCodes.Status201Created,
                ApiResponse.Failed(
                    code: "EMAIL_CONFIRMATION",
                    message: "User registered successfully, but email confirmation failed to send",
                    details: new ReadOnlyDictionary<string, string[]>(new Dictionary<string, string[]>
                    {
                        { ex.Source ?? "error", [ex.Message] }
                    }))
            );
        }
    }

    [HttpPost("signin")]
    public async Task<ActionResult<ApiResponse>> SignIn([FromBody] SignInDto signInDto)
    {
        var user = await _userManager.FindByEmailAsync(signInDto.Email);
        if (user is null)
        {
            return Unauthorized(ApiResponse.Failed(message: "User doesn't exist"));
        }

        if (user.Status == AccountStatus.Disabled)
        {
            return Unauthorized(ApiResponse.Failed(message: "Account disabled"));
        }

        if (!await _signInManager.CanSignInAsync(user))
        {
            return Unauthorized(new { message = "Cannot sign in currently." });
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, signInDto.Password, true);
        if (!result.Succeeded)
        {
            return Unauthorized(new { message = "Invalid email or password." });
        }

        var oldRefreshToken = _refreshTokenService.GetCookie(Request.Cookies);
        if (oldRefreshToken is not null)
        {
            var oldRefreshTokenHash = _refreshTokenService.Hash(oldRefreshToken);
            var oldRefreshTokenRecord = await _dbContext.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.TokenHash == oldRefreshTokenHash
                                           && !rt.IsRevoked
                );

            if (oldRefreshTokenRecord is not null)
            {
                await _refreshTokenService.RevokeFamilyAsync(user.Id, oldRefreshTokenRecord.FamilyId);
            }
        }

        var roles = await _userManager.GetRolesAsync(user);

        var accessTokenMaxAge = _jwtOptions.Expiry;
        var accessTokenExpiresAt = DateTimeOffset.UtcNow.Add(_jwtOptions.Expiry);
        var accessToken = _accessTokenService.Generate(user, roles, accessTokenExpiresAt);

        var refreshTokenMaxAge = signInDto.RememberMe
            ? _refreshTokenOptions.LongExpiry
            : _refreshTokenOptions.ShortExpiry;
        var refreshTokenExpiresAt = DateTimeOffset.UtcNow.Add(refreshTokenMaxAge);
        var newRefreshToken = _refreshTokenService.GeneratePlain();
        var newRefreshTokenHash = _refreshTokenService.Hash(newRefreshToken);
        var newRefreshTokenRecord = new RefreshToken
        {
            UserId = user.Id,
            FamilyId = Guid.NewGuid(),
            TokenHash = newRefreshTokenHash,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = refreshTokenExpiresAt,
            IsPersistent = signInDto.RememberMe,
            Ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            UserAgent = Request.Headers.UserAgent.ToString(),
        };
        await _dbContext.RefreshTokens.AddAsync(newRefreshTokenRecord);
        await _dbContext.SaveChangesAsync();

        _accessTokenService.SetCookie(
            Response.Cookies,
            accessToken,
            accessTokenMaxAge,
            accessTokenExpiresAt
        );
        _refreshTokenService.SetCookie(
            Response.Cookies,
            newRefreshToken,
            refreshTokenMaxAge,
            refreshTokenExpiresAt
        );

        return Ok(ApiResponse.Succeeded(message: "Signed in successfully"));
    }

    [HttpPost("reset-password-intent")]
    public async Task<ActionResult<ApiResponse>> ResetPasswordIntent(
        [FromBody] ResetPasswordIntentDto resetPasswordIntentDto)
    {
        var user = await _userManager.FindByEmailAsync(resetPasswordIntentDto.Email);
        if (user is not null)
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
            var escapedUserId = Uri.EscapeDataString(user.Id.ToString());
            var link =
                $"{_frontendOptions.Origin}/{ResetPasswordPath}?token={encodedToken}&userId={escapedUserId}";

            await _emailService.SendTemplatedEmailAsync(
                EEmailTemplate.ResetPassword, new { first_name = user.FirstName, link }, user.Email!);
        }

        // Same answer whether the account exists or not.
        return Ok(ApiResponse.Succeeded(message: "If an account exists for that email, a reset link has been sent."));
    }

    [HttpPost("reset-password")]
    public async Task<ActionResult<ApiResponse>> ResetPassword([FromBody] ResetPasswordDto resetPasswordDto)
    {
        if (resetPasswordDto.NewPassword != resetPasswordDto.ConfirmedNewPassword)
        {
            return BadRequest(ApiResponse.Failed(
                code: "RESET_PASSWORD",
                message: "Passwords do not match")
            );
        }

        string token;
        try
        {
            token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(resetPasswordDto.Token));
        }
        catch (FormatException)
        {
            return BadRequest(ApiResponse.Failed(
                code: "RESET_LINK_INVALID",
                message: "This reset link is invalid or has expired.")
            );
        }

        var user = await _userManager.FindByIdAsync(resetPasswordDto.UserId.ToString());
        if (user is null)
        {
            return BadRequest(ApiResponse.Failed(
                code: "RESET_LINK_INVALID",
                message: "This reset link is invalid or has expired.")
            );
        }

        var result = await _userManager.ResetPasswordAsync(user, token, resetPasswordDto.NewPassword);
        if (!result.Succeeded)
        {
            if (result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.InvalidToken)))
            {
                return BadRequest(ApiResponse.Failed(
                    code: "RESET_LINK_INVALID",
                    message: "This reset link is invalid or has expired.")
                );
            }

            return BadRequest(ApiResponse.Failed(
                code: "RESET_PASSWORD",
                message: "Password does not meet requirements",
                details: result.ToDetails())
            );
        }

        await _userManager.ResetAccessFailedCountAsync(user);
        await _refreshTokenService.RevokeAllForUserAsync(user.Id);
        try
        {
            await _emailService.SendTemplatedEmailAsync(
                EEmailTemplate.PasswordHasBeenReset,
                new
                {
                    first_name = user.FirstName,
                },
                user.Email!);
        }
        catch (Exception)
        {
            _logger.LogError(
                "Could not send {EmailTemplate} email to user {UserId}",
                nameof(EEmailTemplate.PasswordHasBeenReset),
                user.Id);
        }

        return Ok(ApiResponse.Succeeded(message: "Password updated"));
    }

    [HttpPost("resend-email-confirmation")]
    public async Task<ActionResult<ApiResponse>> ResendEmailConfirmation(
        [FromBody] ResendEmailConfirmationDto resendEmailConfirmationDto)
    {
        var user = await _userManager.FindByEmailAsync(resendEmailConfirmationDto.Email);
        if (user is null)
        {
            return BadRequest(ApiResponse.Failed(message: "No user exists with this email"));
        }

        if (user.EmailConfirmed)
        {
            return BadRequest(ApiResponse.Failed(message: "Email already confirmed"));
        }

        try
        {
            await SendConfirmationEmailAsync(user);
            return Ok(ApiResponse.Succeeded(message: "User registered successfully"));
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse.Failed(
                code: "EMAIL_CONFIRMATION",
                message: "Email confirmation failed to send",
                details: new ReadOnlyDictionary<string, string[]>(new Dictionary<string, string[]>
                {
                    { ex.Source ?? "error", [ex.Message] }
                })));
        }
    }

    [HttpGet("confirm-email")]
    public async Task<ActionResult<ApiResponse>> ConfirmEmail([FromQuery] EmailConfirmationDto dto)
    {
        var user = await _userManager.FindByIdAsync(dto.UserId);
        if (user is null)
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

    private async Task SendConfirmationEmailAsync(ApplicationUser user)
    {
        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var link = _linkGenerator.GetUriByAction(
            httpContext: HttpContext,
            action: nameof(ConfirmEmail),
            controller: AuthController.ControllerName,
            values: new EmailConfirmationDto(user.Id.ToString(), Uri.EscapeDataString(token)),
            scheme: Request.Scheme
        );

        await _emailService.SendTemplatedEmailAsync(
            EEmailTemplate.EmailConfirmation,
            new { first_name = user.FirstName, link },
            user.Email!
        );
    }
}