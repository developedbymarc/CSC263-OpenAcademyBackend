using System.ComponentModel.DataAnnotations;
using OpenAcademy.Entities.Users;
using OpenAcademy.Entities.Users.RoleProfiles;

namespace OpenAcademy.Entities.Courses;

public class Course : ITimestamped
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [Required] public Guid InstructorId { get; set; }
    public InstructorProfile InstructorProfile { get; set; } = null!;

    [Required] [MaxLength(128)] public string Title { get; set; } = null!;
    [Required] [MaxLength(1024)] public string Description { get; set; } = null!;
    public CourseStatus Status { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<Category> Categories { get; } = new List<Category>();
    public ICollection<Module> Modules { get; } = new List<Module>();
    public ICollection<Enrollment> Enrollments { get; } = new List<Enrollment>();
}