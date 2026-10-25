using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using OpenAcademy.Entities.Courses;

namespace OpenAcademy.Entities.Users.RoleProfiles;

public class InstructorProfile
{
    [Key] [ForeignKey("User")] public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    // All optional: the (empty) profile is created at registration, the instructor fills it in later.
    [MaxLength(128)] public string? Headline { get; set; }
    [MaxLength(4000)] public string? Bio { get; set; }
    [MaxLength(512)] public string? Expertise { get; set; }

    public ICollection<Course> Courses { get; } = new List<Course>();
}