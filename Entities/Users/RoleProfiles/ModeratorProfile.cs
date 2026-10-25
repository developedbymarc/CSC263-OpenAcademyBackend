using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using OpenAcademy.Entities.Moderation;

namespace OpenAcademy.Entities.Users.RoleProfiles;

public class ModeratorProfile
{
    [Key] [ForeignKey("User")] public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<Report> AssignedReports { get; } = new List<Report>(); // Report.AssignedTo
}