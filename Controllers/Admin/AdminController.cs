using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OpenAcademy.Configuration.Options;
using OpenAcademy.Entities.Users;
using OpenAcademy.Responses;
using OpenAcademy.Services.ApplicationServices.TokenService;
using OpenAcademy.Services.ExternalServices.EmailService;
using OpenAcademy.Data;
using OpenAcademy.Constants;
using OpenAcademy.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Protocols.Configuration;
using OpenAcademy.Entities.Users.RoleProfiles;
using OpenAcademy.Entities;

namespace OpenAcademy.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.Admin)]
[Route("[controller]")]
public class AdminController : ControllerBase
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
    private const string ControllerName = "Admin";

    public AdminController(
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

    [HttpGet("moderators")]
    public async Task<ActionResult<ApiResponse<IEnumerable<GetAllModeratorsDto>>>> GetAllModerators()
    {
        var moderatorRole = await _dbContext.Roles
            .FirstOrDefaultAsync(r => r.Name == RoleNames.Moderator);

        if (moderatorRole is null)
            throw new InvalidConfigurationException("Moderator role does not exist.");

        var moderators = await _dbContext.UserRoles
            .Where(ur => ur.RoleId == moderatorRole.Id)
            .Join<IdentityUserRole<Guid>, ApplicationUser, Guid, GetAllModeratorsDto>(
                _dbContext.Users.Where(u => u.Status == AccountStatus.Active),
                ur => ur.UserId,
                user => user.Id,
                (ur, user) => new GetAllModeratorsDto(
                    UserId: user.Id,
                    FirstName: user.FirstName,
                    LastName: user.LastName,
                    Email: user.Email!
                ))
            .ToListAsync();

        return Ok(ApiResponse<IEnumerable<GetAllModeratorsDto>>.Succeeded(moderators));
    }

    [HttpPost("create-moderator")]
    public async Task<ActionResult<ApiResponse>> CreateModerator([FromBody] SignUpDto signUpDto)
    {
        // Validate input
        if (signUpDto.Password != signUpDto.ConfirmedPassword)
        {
            return BadRequest(ApiResponse.Failed("Passwords do not match."));
        }

        IdentityResult? failure = null;
        ApplicationUser? createdModerator = null;

        try
        {
            var strategy = _dbContext.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                failure = null;
                createdModerator = null;
                _dbContext.ChangeTracker.Clear();

                await using var tx = await _dbContext.Database.BeginTransactionAsync();

                var moderator = new ApplicationUser
                {
                    UserName = signUpDto.Email,
                    Email = signUpDto.Email,
                    FirstName = signUpDto.FirstName,
                    LastName = signUpDto.LastName,
                    EmailConfirmed = true
                };

                var createResult = await _userManager.CreateAsync(moderator, signUpDto.Password);
                if (!createResult.Succeeded)
                {
                    failure = createResult;
                    await tx.RollbackAsync();
                    return;
                }

                var roleResult = await _userManager.AddToRoleAsync(moderator, RoleNames.Moderator);
                if (!roleResult.Succeeded)
                {
                    failure = roleResult;
                    await tx.RollbackAsync();
                    return;
                }

                await _dbContext.ModeratorProfiles.AddAsync(new ModeratorProfile
                    { UserId = moderator.Id, CreatedAt = DateTimeOffset.UtcNow });
                await _dbContext.SaveChangesAsync();

                await tx.CommitAsync();
                createdModerator = moderator;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Moderator creation failed unexpectedly");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                ApiResponse.Failed(
                    code: "CREATE_MODERATOR",
                    message: "Something went wrong while creating the moderator user",
                    details: failure?.ToDetails()
                )
            );
        }

        if (failure is not null)
        {
            return BadRequest(ApiResponse.Failed(
                code: "CREATE_MODERATOR",
                message: "Could not create a new moderator user",
                details: failure.ToDetails())
            );
        }

        await _emailService.SendTemplatedEmailAsync(
            EEmailTemplate.PrivilegedRoleAssignmentNotification,
            new { first_name = createdModerator!.FirstName, role = RoleNames.Moderator },
            createdModerator.Email!);
        _logger.LogInformation("Moderator user created successfully: {ModeratorEmail}", createdModerator.Email);
        return Ok(ApiResponse.Succeeded("Moderator user created successfully."));
    }

    [HttpDelete("delete-moderator/{moderatorId:guid}")]
    public async Task<ActionResult<ApiResponse>> DeleteModerator([FromRoute] Guid moderatorId)
    {
        IdentityResult? failure = null;
        ApplicationUser? disabledModerator = null;

        try
        {
            var strategy = _dbContext.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                failure = null;
                disabledModerator = null;
                _dbContext.ChangeTracker.Clear();

                await using var tx = await _dbContext.Database.BeginTransactionAsync();

                var moderator = await _userManager.FindByIdAsync(moderatorId.ToString());
                if (moderator is null)
                {
                    await tx.RollbackAsync();
                    return;
                }

                var now = DateTimeOffset.UtcNow;
                moderator.Status = AccountStatus.Disabled;
                moderator.StatusUpdatedAt = now;
                moderator.StatusUpdatedReason = "Account disabled by administrator.";
                moderator.LockoutEnabled = true;
                moderator.LockoutEnd = DateTimeOffset.MaxValue;

                var updateResult = await _userManager.UpdateAsync(moderator);
                if (!updateResult.Succeeded)
                {
                    failure = updateResult;
                    await tx.RollbackAsync();
                    return;
                }

                await _refreshTokenService.RevokeAllForUserAsync(moderator.Id);

                await tx.CommitAsync();
                disabledModerator = moderator;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Moderator deactivation failed unexpectedly");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                ApiResponse.Failed(
                    code: "DELETE_MODERATOR",
                    message: "Something went wrong while disabling the moderator user",
                    details: failure?.ToDetails()
                )
            );
        }

        if (disabledModerator is null && failure is null)
        {
            return NotFound(ApiResponse.Failed("Moderator user not found."));
        }

        if (failure is not null)
        {
            return BadRequest(ApiResponse.Failed(
                code: "DELETE_MODERATOR",
                message: "Could not disable the moderator user",
                details: failure.ToDetails())
            );
        }

        try
        {
            await _emailService.SendTemplatedEmailAsync(
                EEmailTemplate.AccountDeactivationNotification,
                new { first_name = disabledModerator!.FirstName },
                disabledModerator.Email!);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not send deactivation notification for user {UserId}", disabledModerator!.Id);
        }

        _logger.LogInformation("Moderator user disabled successfully: {ModeratorEmail}", disabledModerator!.Email);
        return Ok(ApiResponse.Succeeded("Moderator user disabled successfully."));
    }
}