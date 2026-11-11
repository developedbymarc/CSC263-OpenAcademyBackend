using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OpenAcademy.Configuration.Options;
using OpenAcademy.Data;
using OpenAcademy.Entities.Users;
using OpenAcademy.Services.ApplicationServices.TokenService;
using OpenAcademy.Services.ExternalServices.EmailService;

namespace OpenAcademy.Controllers;

[ApiController]
[Route("[controller]")]
public class CoursesController : ControllerBase
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
    private readonly ILogger<CoursesController> _logger;
    private readonly LinkGenerator _linkGenerator;

    public CoursesController(
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
        ILogger<CoursesController> logger,
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
}