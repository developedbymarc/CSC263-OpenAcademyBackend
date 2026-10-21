using OpenAcademy.Entities.Users;
using OpenAcademy.Entities.Users.RoleProfiles;

namespace OpenAcademy.Entities.Courses;

public class Enrollment
{
    public Guid StudentId { get; set; }
    public StudentProfile StudentProfile { get; set; } = null!;

    public Guid CourseId { get; set; }
    public Course Course { get; set; } = null!;

    public DateTimeOffset EnrollmentDate { get; set; }
    public EnrollmentStatus Status { get; set; }
}