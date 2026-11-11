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
using OpenAcademy.Entities;
using OpenAcademy.Utils;
using System.Collections.ObjectModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Protocols.Configuration;

namespace OpenAcademy.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.Root)]
[Route("[controller]")]
public class RootController : ControllerBase
{
    // services
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly AbstractRefreshTokenService _refreshTokenService;
    private readonly IEmailService _emailService;

    // utils
    private readonly ILogger<RootController> _logger;

    // local constants
    private const string ControllerName = "Root";

    public RootController(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        AbstractRefreshTokenService refreshTokenService,
        IEmailService emailService,
        ILogger<RootController> logger)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _roleManager = roleManager;
        _refreshTokenService = refreshTokenService;
        _emailService = emailService;
        _logger = logger;
    }

    // TODO: Invite instead of provisioning a role.
    //       Or provision a role and make them reset the password
    
    [HttpGet("admins")]
    public async Task<ActionResult<ApiResponse<IEnumerable<GetAllAdminsDto>>>> GetAllAdmins()
    {
        var adminRole = await _dbContext.Roles
            .FirstOrDefaultAsync(r => r.Name == RoleNames.Admin);

        if (adminRole is null)
            throw new InvalidConfigurationException("Admin role does not exist.");

        var admins = await _dbContext.UserRoles
            .Where(ur => ur.RoleId == adminRole.Id)
            .Join<IdentityUserRole<Guid>, ApplicationUser, Guid, GetAllAdminsDto>(
                _dbContext.Users.Where(u => u.Status == AccountStatus.Active),
                ur => ur.UserId,
                user => user.Id,
                (ur, user) => new GetAllAdminsDto(
                    UserId: user.Id,
                    FirstName: user.FirstName,
                    LastName: user.LastName,
                    Email: user.Email!
                ))
            .ToListAsync();

        return Ok(ApiResponse<IEnumerable<GetAllAdminsDto>>.Succeeded(admins));
    }

    [HttpPost("create-admin")]
    public async Task<ActionResult<ApiResponse>> CreateAdmin([FromBody] SignUpDto signUpDto)
    {
        // Validate input
        if (signUpDto.Password != signUpDto.ConfirmedPassword)
        {
            return BadRequest(ApiResponse.Failed("Passwords do not match."));
        }

        IdentityResult? failure = null;
        ApplicationUser? createdAdmin = null;

        try
        {
            var strategy = _dbContext.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                failure = null;
                createdAdmin = null;
                _dbContext.ChangeTracker.Clear();

                await using var tx = await _dbContext.Database.BeginTransactionAsync();

                // Create new admin user
                var admin = new ApplicationUser
                {
                    UserName = signUpDto.Email,
                    Email = signUpDto.Email,
                    FirstName = signUpDto.FirstName,
                    LastName = signUpDto.LastName,
                    EmailConfirmed = true
                };

                var createResult = await _userManager.CreateAsync(admin, signUpDto.Password);
                if (!createResult.Succeeded)
                {
                    failure = createResult;
                    await tx.RollbackAsync();
                    return;
                }

                var roleResult = await _userManager.AddToRoleAsync(admin, RoleNames.Admin);
                if (!roleResult.Succeeded)
                {
                    failure = roleResult;
                    await tx.RollbackAsync();
                    return;
                }

                await _dbContext.AdminProfiles.AddAsync(new AdminProfile()
                    { UserId = admin.Id, CreatedAt = DateTimeOffset.UtcNow });
                await _dbContext.SaveChangesAsync();

                await tx.CommitAsync();
                createdAdmin = admin;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin creation failed unexpectedly");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                ApiResponse.Failed(
                    code: "CREATE_ADMIN",
                    message: "Something went wrong while creating the admin user",
                    details: failure?.ToDetails()
                )
            );
        }

        if (failure is not null)
        {
            return BadRequest(ApiResponse.Failed(
                code: "CREATE_ADMIN",
                message: "Could not create a new admin user",
                details: failure.ToDetails())
            );
        }

        try
        {
            await _emailService.SendTemplatedEmailAsync(
                EEmailTemplate.PrivilegedRoleAssignmentNotification,
                new { first_name = createdAdmin!.FirstName, role = RoleNames.Admin },
                createdAdmin.Email!);

            _logger.LogInformation("Admin user created successfully: {AdminEmail}", createdAdmin.Email);
            return StatusCode(StatusCodes.Status201Created,
                ApiResponse.Succeeded("Admin user created successfully.")
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not send email notification for user {UserId}", createdAdmin!.Id);
            return StatusCode(StatusCodes.Status201Created,
                ApiResponse.Failed(
                    code: "EMAIL_NOTIFICATION",
                    message: "Admin registered successfully, but email notification failed to send",
                    details: new ReadOnlyDictionary<string, string[]>(new Dictionary<string, string[]>
                    {
                        { ex.Source ?? "error", [ex.Message] }
                    }))
            );
        }
    }

    [HttpDelete("delete-admin/{adminId:guid}")]
    public async Task<ActionResult<ApiResponse>> DeleteAdmin([FromRoute] Guid adminId)
    {
        IdentityResult? failure = null;
        ApplicationUser? disabledAdmin = null;

        try
        {
            var strategy = _dbContext.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                failure = null;
                disabledAdmin = null;
                _dbContext.ChangeTracker.Clear();

                await using var tx = await _dbContext.Database.BeginTransactionAsync();

                var admin = await _userManager.FindByIdAsync(adminId.ToString());
                if (admin is null)
                {
                    await tx.RollbackAsync();
                    return;
                }

                var now = DateTimeOffset.UtcNow;
                admin.Status = AccountStatus.Disabled;
                admin.StatusUpdatedAt = now;
                admin.StatusUpdatedReason = "Account disabled by root administrator.";
                admin.LockoutEnabled = true;
                admin.LockoutEnd = DateTimeOffset.MaxValue;

                var updateResult = await _userManager.UpdateAsync(admin);
                if (!updateResult.Succeeded)
                {
                    failure = updateResult;
                    await tx.RollbackAsync();
                    return;
                }

                await _refreshTokenService.RevokeAllForUserAsync(admin.Id);

                await tx.CommitAsync();
                disabledAdmin = admin;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin deactivation failed unexpectedly");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                ApiResponse.Failed(
                    code: "DELETE_ADMIN",
                    message: "Something went wrong while disabling the admin user",
                    details: failure?.ToDetails()
                )
            );
        }

        if (disabledAdmin is null && failure is null)
        {
            return NotFound(ApiResponse.Failed("Admin user not found."));
        }

        if (failure is not null)
        {
            return BadRequest(ApiResponse.Failed(
                code: "DELETE_ADMIN",
                message: "Could not disable the admin user",
                details: failure.ToDetails())
            );
        }

        try
        {
            await _emailService.SendTemplatedEmailAsync(
                EEmailTemplate.AccountDeactivationNotification,
                new { first_name = disabledAdmin!.FirstName },
                disabledAdmin.Email!);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not send deactivation notification for user {UserId}", disabledAdmin!.Id);
        }

        _logger.LogInformation("Admin user disabled successfully: {AdminEmail}", disabledAdmin!.Email);
        return Ok(ApiResponse.Succeeded("Admin user disabled successfully."));
    }
}