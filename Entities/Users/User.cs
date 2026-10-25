using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using OpenAcademy.Entities.Administration;
using OpenAcademy.Entities.Moderation;
using OpenAcademy.Entities.Tokens;
using OpenAcademy.Entities.Users.RoleProfiles;

namespace OpenAcademy.Entities.Users;

public class ApplicationUser : IdentityUser<Guid>
{
    // IdentityUser<Guid> does not assign an Id by itself, so the Id exists as soon as the object does.
    public ApplicationUser()
    {
        Id = Guid.NewGuid();
    }

    // personal information
    [Required] [MaxLength(64)] public string FirstName { get; set; } = null!;
    [Required] [MaxLength(64)] public string LastName { get; set; } = null!;

    // metadata
    public DateTimeOffset CreatedAt { get; set; }

    // moderation
    public AccountStatus Status { get; set; }
    public DateTimeOffset? StatusUpdatedAt { get; set; }
    [MaxLength(2048)] public string? StatusUpdatedReason { get; set; }

    // role profiles (each is 0..1: it exists only if the user holds that role)
    public StudentProfile? StudentProfile { get; set; }
    public InstructorProfile? InstructorProfile { get; set; }
    public ModeratorProfile? ModeratorProfile { get; set; }
    public AdminProfile? AdminProfile { get; set; }

    // auth
    public ICollection<RefreshToken> RefreshTokens { get; } = new List<RefreshToken>();

    // moderation: two different relationships between User and Report
    public ICollection<Report> IssuedReports { get; } = new List<Report>(); // Report.Issuer
    public ICollection<Report> ClosedReports { get; } = new List<Report>(); // Report.ClosedBy

    // administration
    public ICollection<AuditLog> AuditLogs { get; } = new List<AuditLog>(); // AuditLog.Actor
}