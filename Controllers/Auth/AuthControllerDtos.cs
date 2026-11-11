using OpenAcademy.Responses;
using OpenAcademy.Controllers;
using OpenAcademy.Entities.Users;

namespace OpenAcademy.Controllers;

public sealed record WhoAmIDto(
    string UserId,
    string FirstName,
    string LastName,
    string Email,
    string Role,
    string? AvatarUrl);

public sealed record CsrfTokenDto(string? RequestToken);

public sealed record EmailConfirmationDto(string UserId, string Token);

public sealed record ResendEmailConfirmationDto(string Email);

public sealed record SignUpDto(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    string ConfirmedPassword,
    string Role);

public sealed record SignInDto(string Email, string Password, bool RememberMe);

public sealed record DeviceSessionDto(
    string RefreshTokenId,
    string RefreshTokenFamilyId,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    bool IsPersistent,
    string Ip,
    string UserAgent);

public sealed record SignOutDeviceDto(Guid RefreshTokenFamilyId);

public sealed record ResetPasswordIntentDto(string Email);

public sealed record ResetPasswordDto(Guid UserId, string Token, string NewPassword, string ConfirmedNewPassword);