namespace OpenAcademy.Controllers;

public sealed record GetAllModeratorsDto(Guid UserId, string FirstName, string LastName, string Email);