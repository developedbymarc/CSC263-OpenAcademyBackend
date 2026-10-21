using System.ComponentModel.DataAnnotations;

namespace OpenAcademy.Entities.Courses;

public class Module : ITimestamped
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CourseId { get; set; }
    public Course Course { get; set; } = null!;

    [Required] [MaxLength(128)] public string Title { get; set; } = null!;
    [Required] [MaxLength(1024)] public string Description { get; set; } = null!;

    [Range(0, int.MaxValue)] public int Order { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<Lesson> Lessons { get; } = new List<Lesson>();
}