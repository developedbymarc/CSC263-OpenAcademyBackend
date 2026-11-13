namespace OpenAcademy.Controllers;

public sealed record GeneralUserInfoDto(string FirstName, string LastName);
public sealed record GeneralUserInfoWithAvatarUrlDto(string FirstName, string LastName, string? AvatarUrl);
public sealed record StudentProfileInfoDto();
public sealed record InstructorProfileInfoDto(string Headline, string Bio, string Expertise);
public sealed record ModeratorProfileInfoDto();
public sealed record AdminProfileInfoDto();
public sealed record RootProfileInfoDto();
public sealed record UpdateMyInfoDto(
    GeneralUserInfoDto GeneralInfo,
    StudentProfileInfoDto? StudentProfile,
    InstructorProfileInfoDto? InstructorProfile,
    ModeratorProfileInfoDto? ModeratorProfile,
    AdminProfileInfoDto? AdminProfile,
    RootProfileInfoDto? RootProfile
);
public record struct GetMyInfoDto(
    GeneralUserInfoWithAvatarUrlDto GeneralInfo,
    StudentProfileInfoDto? StudentProfile,
    InstructorProfileInfoDto? InstructorProfile,
    ModeratorProfileInfoDto? ModeratorProfile,
    AdminProfileInfoDto? AdminProfile,
    RootProfileInfoDto? RootProfile
);
