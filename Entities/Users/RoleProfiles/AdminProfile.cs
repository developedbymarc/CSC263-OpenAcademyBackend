using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using OpenAcademy.Entities.Moderation;

namespace OpenAcademy.Entities.Users.RoleProfiles;

public class AdminProfile
{
    [Key] [ForeignKey("User")] public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<Report> ElevatedReports { get; } = new List<Report>(); // Report.ElevatedTo
}