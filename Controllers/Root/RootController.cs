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
using Microsoft.AspNetCore.Authorization;

namespace OpenAcademy.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.Root)]
[Route("[controller]")]
public class RootController : ControllerBase
{
    // services
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailService _emailService;

    // utils
    private readonly ILogger<RootController> _logger;

    // local constants
    private const string ControllerName = "Root";

    public RootController(
        UserManager<ApplicationUser> userManager,
        IEmailService emailService,
        ILogger<RootController> logger)
    {
        _userManager = userManager;
        _emailService = emailService;
        _logger = logger;
    }

    [HttpGet("admins")]
    public async Task<ActionResult<ApiResponse<IEnumerable<ApplicationUser>>>> GetAllAdmins()
    {
        var admins = await _userManager.GetUsersInRoleAsync(RoleNames.Admin);
        return Ok(ApiResponse<IEnumerable<ApplicationUser>>.Succeeded(admins));
    }

    [HttpPost("create-admin")]
    public async Task<ActionResult<ApiResponse>> CreateAdmin([FromBody] SignUpDto signUpDto)
    {
        // Validate input
        if (signUpDto.Password != signUpDto.ConfirmedPassword)
        {
            return BadRequest(ApiResponse.Failed("Passwords do not match."));
        }

        // Create new admin user
        var newAdmin = new ApplicationUser
        {
            UserName = signUpDto.Email,
            Email = signUpDto.Email,
            FirstName = signUpDto.FirstName,
            LastName = signUpDto.LastName,
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(newAdmin, signUpDto.Password);
        if (!result.Succeeded)
        {
            return BadRequest(ApiResponse.Failed(
                code: "CREATE_ADMIN",
                message: "Could not create a new admin user",
                details: result.ToDetails())
            );
        }

        // Assign admin role
        await _userManager.AddToRoleAsync(newAdmin, RoleNames.Admin);
        await _emailService.SendTemplatedEmailAsync(
            EEmailTemplate.PrivilegedRoleAssignmentNotification,
            new { first_name = newAdmin.FirstName, role = RoleNames.Admin },
            newAdmin.Email);
        _logger.LogInformation("Admin user created successfully: {AdminEmail}", newAdmin.Email);
        return Ok(ApiResponse.Succeeded("Admin user created successfully."));
    }

    [HttpDelete("delete-admin/{adminId:guid}")]
    public async Task<ActionResult<ApiResponse>> DeleteAdmin([FromRoute] Guid adminId)
    {
        var adminUser = await _userManager.FindByIdAsync(adminId.ToString());
        if (adminUser == null)
        {
            return NotFound(ApiResponse.Failed("Admin user not found."));
        }

        var result = await _userManager.DeleteAsync(adminUser);
        if (!result.Succeeded)
        {
            return BadRequest(ApiResponse.Failed(
                code: "DELETE_ADMIN",
                message: "Could not delete the admin user",
                details: result.ToDetails())
            );
        }
        await _emailService.SendTemplatedEmailAsync(
            EEmailTemplate.PrivilegedRoleRevocationNotification,
            new { first_name = adminUser.FirstName, role = RoleNames.Admin },
            adminUser.Email!);
        _logger.LogInformation("Admin user deleted successfully: {AdminEmail}", adminUser.Email);
        return Ok(ApiResponse.Succeeded("Admin user deleted successfully."));
    }
}
