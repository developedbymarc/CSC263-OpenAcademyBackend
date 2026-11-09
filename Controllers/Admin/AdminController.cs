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
    public async Task<ActionResult<ApiResponse<IEnumerable<ApplicationUser>>>> GetAllModerators()
    {
        var moderators = await _userManager.GetUsersInRoleAsync(RoleNames.Moderator);
        return Ok(ApiResponse<IEnumerable<ApplicationUser>>.Succeeded(moderators));
    }

    [HttpPost("create-moderator")]
    public async Task<ActionResult<ApiResponse>> CreateModerator([FromBody] SignUpDto signUpDto)
    {
        // Validate input
        if (signUpDto.Password != signUpDto.ConfirmedPassword)
        {
            return BadRequest(ApiResponse.Failed("Passwords do not match."));
        }

        // Create new moderator user
        var newModerator = new ApplicationUser
        {
            UserName = signUpDto.Email,
            Email = signUpDto.Email,
            FirstName = signUpDto.FirstName,
            LastName = signUpDto.LastName,
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(newModerator, signUpDto.Password);
        if (!result.Succeeded)
        {
            return BadRequest(ApiResponse.Failed(
                code: "CREATE_MODERATOR",
                message: "Could not create a new moderator user",
                details: result.ToDetails())
            );
        }

        // Assign moderator role
        await _userManager.AddToRoleAsync(newModerator, RoleNames.Moderator);
        await _emailService.SendTemplatedEmailAsync(
            EEmailTemplate.PrivilegedRoleAssignmentNotification,
            new { first_name = newModerator.FirstName, role = RoleNames.Moderator },
            newModerator.Email);
        _logger.LogInformation("Moderator user created successfully: {ModeratorEmail}", newModerator.Email);
        return Ok(ApiResponse.Succeeded("Moderator user created successfully."));
    }

    [HttpDelete("delete-moderator/{moderatorId:guid}")]
    public async Task<ActionResult<ApiResponse>> DeleteModerator([FromRoute] Guid moderatorId)
    {
        var moderatorUser = await _userManager.FindByIdAsync(moderatorId.ToString());
        if (moderatorUser == null)
        {
            return NotFound(ApiResponse.Failed("Moderator user not found."));
        }

        var result = await _userManager.DeleteAsync(moderatorUser);
        if (!result.Succeeded)
        {
            return BadRequest(ApiResponse.Failed(
                code: "DELETE_MODERATOR",
                message: "Could not delete the moderator user",
                details: result.ToDetails())
            );
        }
        await _emailService.SendTemplatedEmailAsync(
            EEmailTemplate.PrivilegedRoleRevocationNotification,
            new { first_name = moderatorUser.FirstName, role = RoleNames.Moderator },
            moderatorUser.Email!);
        _logger.LogInformation("Moderator user deleted successfully: {ModeratorEmail}", moderatorUser.Email);
        return Ok(ApiResponse.Succeeded("Moderator user deleted successfully."));
    }
}
