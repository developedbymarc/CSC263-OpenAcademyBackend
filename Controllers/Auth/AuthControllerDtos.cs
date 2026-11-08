using OpenAcademy.Responses;
using OpenAcademy.Controllers;

namespace OpenAcademy.Controllers;

public sealed record CsrfTokenDto(string? RequestToken);
public sealed record EmailConfirmationDto(string UserId, string Token);
