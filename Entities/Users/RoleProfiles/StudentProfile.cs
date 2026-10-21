using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using OpenAcademy.Entities.Courses;

namespace OpenAcademy.Entities.Users.RoleProfiles;

public class StudentProfile
{
    [Key] [ForeignKey("User")] public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public ICollection<Enrollment> Enrollments { get; } = new List<Enrollment>();
}