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
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.WebUtilities;
using OpenAcademy.Entities;
using OpenAcademy.Services.ExternalServices.MediaService;

namespace OpenAcademy.Controllers;

[Authorize]
[ApiController]
[Route("[controller]")]
public class MeController : ControllerBase
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
    private readonly IMediaService _mediaService;

    // options
    private readonly JwtOptions _jwtOptions;
    private readonly RefreshTokenOptions _refreshTokenOptions;
    private readonly FrontendOptions _frontendOptions;
    private readonly MediaOptions _mediaOptions;

    // utils
    private readonly ILogger<MeController> _logger;
    private readonly LinkGenerator _linkGenerator;

    // local constants
    private const string ControllerName = "Me";
    private const string ResetPasswordPath = "/reset-password";
    private const string SignInPath = "/sign-in";

    public MeController(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        SignInManager<ApplicationUser> signInManager,
        AbstractAccessTokenService accessTokenService,
        AbstractRefreshTokenService refreshTokenService,
        IAntiforgery antiforgery,
        IEmailService emailService,
        IMediaService mediaService,
        IOptions<FrontendOptions> frontendOptions,
        IOptions<MediaOptions> mediaOptions,
        IOptions<JwtOptions> jwtOptions,
        IOptions<RefreshTokenOptions> refreshTokenOptions,
        ILogger<MeController> logger,
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
        _mediaService = mediaService;
        _frontendOptions = frontendOptions.Value;
        _mediaOptions = mediaOptions.Value;
        _jwtOptions = jwtOptions.Value;
        _refreshTokenOptions = refreshTokenOptions.Value;
        _logger = logger;
        _linkGenerator = linkGenerator;
    }

    private Guid GetCurrentUserId()
    {
        // same fallback as your authorization handler: "sub" is not always mapped to NameIdentifier
        var raw = _userManager.GetUserId(User) ?? User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        var id = Guid.Parse(raw!);
        return id;
    }

    private string GetCurrentUserRole()
    {
        return User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
    }

    // Change profile info
    [HttpPut("info")]
    public async Task<ActionResult<ApiResponse>> UpdateMyInfo([FromBody] UpdateMyInfoDto myInfoDto)
    {
        var userId = GetCurrentUserId();
        var role = GetCurrentUserRole();

        var user = await _dbContext.Users
            .Include(u => u.StudentProfile)
            .Include(u => u.InstructorProfile)
            .Include(u => u.ModeratorProfile)
            .Include(u => u.AdminProfile)
            .Include(u => u.RootProfile)
            .FirstOrDefaultAsync(u => u.Id == userId);

        user!.FirstName = myInfoDto.GeneralInfo.FirstName;
        user.LastName = myInfoDto.GeneralInfo.LastName;

        switch (role)
        {
            case RoleNames.Student:
                break;

            case RoleNames.Instructor:
                if (myInfoDto.InstructorProfile is null)
                {
                    return BadRequest(ApiResponse.Failed(
                        code: "MISSING_INSTRUCTOR_PROFILE",
                        message: "Instructor profile information is required for users with the Instructor role."));
                }
                user.InstructorProfile!.Headline = myInfoDto.InstructorProfile.Headline;
                user.InstructorProfile!.Bio = myInfoDto.InstructorProfile.Bio;
                user.InstructorProfile!.Expertise = myInfoDto.InstructorProfile.Expertise;
                break;

            case RoleNames.Moderator:
                break;
            case RoleNames.Admin:
                break;
            case RoleNames.Root:
                break;
            default:
                return BadRequest(ApiResponse.Failed(
                    code: "INVALID_ROLE",
                    message: "The user's role is invalid or not supported."));
        }

        _dbContext.Users.Update(user);
        await _dbContext.SaveChangesAsync();

        return Ok();
    }

    // no explicit request size limit here for the time being.
    // we'll just rely on the cloudflare tunnel, nginx, and kestrel limits for now.
    [HttpPut("avatar")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ApiResponse>> UpdateMyAvatar([FromForm] IFormFile profileImage)
    {
        var userId = GetCurrentUserId().ToString();

        try
        {
            await _mediaService.StorePublicImageAsync($"avatar/{userId}", profileImage, HttpContext.RequestAborted);
        }
        catch (MediaRejectedException ex)
        {
            return StatusCode(
                ex.StatusCode,
                ApiResponse.Failed(
                    code: "MEDIA_UPLOAD",
                    message: ex.Message
                )
            );
        }

        return Ok(ApiResponse.Succeeded(message: "Profile image updated successfully."));
    }

    [HttpGet("info")]
    public async Task<ActionResult<GetMyInfoDto>> GetMyInfo()
    {
        var userId = GetCurrentUserId();
        var role = GetCurrentUserRole();

        var user = await _dbContext.Users
            .Include(u => u.StudentProfile)
            .Include(u => u.InstructorProfile)
            .Include(u => u.ModeratorProfile)
            .Include(u => u.AdminProfile)
            .Include(u => u.RootProfile)
            .FirstOrDefaultAsync(u => u.Id == userId);



        var myInfoDto = new GetMyInfoDto
        (
            GeneralInfo: new GeneralUserInfoWithAvatarUrlDto
            (
                FirstName: user!.FirstName,
                LastName: user.LastName,
                AvatarUrl: string.IsNullOrEmpty(user.AvatarBlobPath)
                    ? null
                    : _mediaService.GetPublicUrl(user.AvatarBlobPath)
            ),
            new StudentProfileInfoDto(),
            new InstructorProfileInfoDto(
                Headline: string.Empty,
                Bio: string.Empty,
                Expertise: string.Empty
            ),
            new ModeratorProfileInfoDto(),
            new AdminProfileInfoDto(),
            new RootProfileInfoDto()
        );

        switch (role)
        {
            case RoleNames.Student:
                break;

            case RoleNames.Instructor:
                if (user.InstructorProfile is not null)
                {
                    myInfoDto.InstructorProfile = new InstructorProfileInfoDto(
                        Headline: user.InstructorProfile.Headline!,
                        Bio: user.InstructorProfile.Bio!,
                        Expertise:user.InstructorProfile.Expertise!
                    );
                }
                break;

            case RoleNames.Moderator:
                break;
            case RoleNames.Admin:
                break;
            case RoleNames.Root:
                break;
            default:
                return BadRequest(ApiResponse.Failed(
                    code: "INVALID_ROLE",
                    message: "The user's role is invalid or not supported."));
        }

        return Ok(myInfoDto);
    }
}
