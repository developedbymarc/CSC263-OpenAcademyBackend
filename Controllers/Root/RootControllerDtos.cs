namespace OpenAcademy.Controllers;

public sealed record GetAllAdminsDto(Guid UserId, string FirstName, string LastName, string Email);